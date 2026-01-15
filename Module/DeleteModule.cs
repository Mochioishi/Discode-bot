using Discord;
using Discord.Interactions;
using Discord_bot.Infrastructure;
using Dapper;
using System.Collections.Concurrent;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Discord_bot.Module
{
    public class DeleteModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly DbConfig _db;
        // ユーザーごとの削除開始地点を一時保持
        private static readonly ConcurrentDictionary<ulong, ulong> _deleteStarts = new();

        public DeleteModule(DbConfig db) => _db = db;

        // --- 1. 自動削除設定 (午前4時実行用) ---

        [SlashCommand("deleteago", "X日経過したメッセージを午前4時に自動削除する設定")]
        public async Task SetDeleteAgo(
            [Summary("days", "何日前までのメッセージを残すか")] 
            [Choice("1日前", 1), Choice("2日前", 2), Choice("3日前", 3), Choice("7日前", 7)] int days,
            [Summary("protect", "削除から保護する対象")]
            [Choice("なし", 0), Choice("画像あり", 1), Choice("リアクションあり", 2), Choice("画像またはリアクションあり", 3)] int protect = 0)
        {
            await DeferAsync(ephemeral: true);

            try
            {
                using var conn = _db.GetConnection();
                const string sql = @"
                    INSERT INTO DeleteConfigs (ChannelId, GuildId, Days, ProtectType) 
                    VALUES (@cid, @gid, @d, @p) 
                    ON CONFLICT (ChannelId) 
                    DO UPDATE SET Days = @d, ProtectType = @p";

                await conn.ExecuteAsync(sql, new { 
                    cid = (long)Context.Channel.Id, 
                    gid = (long)Context.Guild.Id, 
                    d = days, 
                    p = protect 
                });
                
                string pText = protect switch { 1 => "画像", 2 => "リアクション", 3 => "画像/リアクション", _ => "なし" };
                await FollowupAsync($"✅ 設定完了: {days}日以上前のメッセージを毎日午前4時に削除します。(保護: {pText})", ephemeral: true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DeleteAgo Error] {ex}");
                await FollowupAsync("❌ 設定の保存中にエラーが発生しました", ephemeral: true);
            }
        }

        [SlashCommand("deleteago_list", "自動削除設定の一覧表示")]
        public async Task DeleteAgoList()
        {
            await DeferAsync(ephemeral: true);

            using var conn = _db.GetConnection();
            const string sql = "SELECT * FROM DeleteConfigs WHERE GuildId = @gid";
            // PostgreSQLではカラム名が小文字で返ることがあるため、dynamicで受ける
            var configs = (await conn.QueryAsync(sql, new { gid = (long)Context.Guild.Id })).ToList();

            if (!configs.Any())
            {
                await FollowupAsync("自動削除が設定されているチャンネルはありません", ephemeral: true);
                return;
            }

            var embed = new EmbedBuilder().WithTitle("🗑️ 自動削除設定一覧").WithColor(Color.Red);
            var builder = new ComponentBuilder();

            foreach (var c in configs)
            {
                // dynamic型のプロパティ名は大文字小文字を区別しないか、小文字でアクセス
                var channelId = (ulong)(long)c.channelid;
                var days = (int)c.days;
                var protectType = (int)c.protecttype;

                var channel = Context.Guild.GetChannel(channelId);
                string channelName = channel?.Name ?? $"ID:{channelId}";

                string pText = protectType switch { 1 => "画像", 2 => "リアクション", 3 => "画像/リアクション", _ => "なし" };
                embed.AddField($"#{channelName}", $"{days}日前を削除 / 保護: {pText}");
                
                builder.WithButton($"設定削除: #{channelName}", $"delago_rmv_{channelId}", ButtonStyle.Danger);
            }

            await FollowupAsync(embed: embed.Build(), components: builder.Build(), ephemeral: true);
        }

        // --- 2. 右クリック範囲削除 (Context Menu) ---

        [MessageCommand("🚩 開始場所")]
        public async Task SetRangeStart(IMessage msg)
        {
            _deleteStarts[Context.User.Id] = msg.Id;
            await RespondAsync("📍 開始地点を記憶しました。", ephemeral: true);
        }

        [MessageCommand("🚩 終了場所")]
        public async Task SetRangeEnd(IMessage msg)
        {
            if (!_deleteStarts.TryGetValue(Context.User.Id, out var startId))
            {
                await RespondAsync("❌ 先に「🚩 開始場所」を選択してください。", ephemeral: true);
                return;
            }

            var menu = new SelectMenuBuilder()
                .WithCustomId($"range_exec:{startId}:{msg.Id}")
                .WithPlaceholder("保護ルールを選択して削除実行")
                .AddOption("なし（すべて削除）", "0")
                .AddOption("画像を保護", "1")
                .AddOption("リアクションを保護", "2")
                .AddOption("画像とリアクションを保護", "3");

            await RespondAsync("削除範囲の保護ルールを選択してください：", 
                components: new ComponentBuilder().WithSelectMenu(menu).Build(), ephemeral: true);
        }

        [ComponentInteraction("range_exec:*:*")]
        public async Task ExecuteRangeDelete(string startIdStr, string endIdStr, string[] selectedValues)
        {
            await DeferAsync(ephemeral: true);
            
            try
            {
                ulong startId = ulong.Parse(startIdStr);
                ulong endId = ulong.Parse(endIdStr);
                int protect = int.Parse(selectedValues[0]);

                var minId = Math.Min(startId, endId);
                var maxId = Math.Max(startId, endId);

                // メッセージ取得
                var messages = await Context.Channel.GetMessagesAsync(minId, Direction.After, 100).FlattenAsync();
                var targetMsgs = messages.Where(m => m.Id <= maxId).ToList();
                
                var startMsg = await Context.Channel.GetMessageAsync(minId);
                if (startMsg != null) targetMsgs.Add(startMsg);
                
                if (!targetMsgs.Any(m => m.Id == maxId))
                {
                    var endMsg = await Context.Channel.GetMessageAsync(maxId);
                    if (endMsg != null) targetMsgs.Add(endMsg);
                }

                var toDelete = targetMsgs.Where(m => {
                    bool hasImage = m.Attachments.Any(a => a.ContentType?.StartsWith("image/") == true);
                    bool hasReaction = m.Reactions.Count > 0;

                    return protect switch {
                        1 => !hasImage,
                        2 => !hasReaction,
                        3 => !hasImage && !hasReaction,
                        _ => true
                    };
                }).ToList();

                if (Context.Channel is ITextChannel ch && toDelete.Any())
                {
                    await ch.DeleteMessagesAsync(toDelete);
                    await FollowupAsync($"🗑️ {toDelete.Count}件のメッセージを削除", ephemeral: true);
                }
                else
                {
                    await FollowupAsync("削除対象のメッセージが見つかりませんでした", ephemeral: true);
                }
                
                _deleteStarts.TryRemove(Context.User.Id, out _);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RangeDelete Error] {ex}");
                await FollowupAsync("❌ 削除中にエラーが発生しました。メッセージが古すぎる（2週間以上前）可能性があります", ephemeral: true);
            }
        }

        [ComponentInteraction("delago_rmv_*")]
        public async Task RemoveDeleteAgo(string channelId)
        {
            await DeferAsync(ephemeral: true);
            using var conn = _db.GetConnection();
            await conn.ExecuteAsync("DELETE FROM DeleteConfigs WHERE ChannelId = @cid", new { cid = long.Parse(channelId) });
            await FollowupAsync("✅ 自動削除設定を解除しました", ephemeral: true);
        }
    }
}

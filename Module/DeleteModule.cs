using Discord;
using Discord.Interactions;
using Discord_bot.Infrastructure;
using Dapper;
using System.Collections.Concurrent;

namespace Discord_bot.Module
{
    public class DeleteModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly DbConfig _db;
        // ユーザーごとの削除開始地点を一時保持（再起動でリセットされますが、DBを汚さないための設計）
        private static readonly ConcurrentDictionary<ulong, ulong> _deleteStarts = new();

        public DeleteModule(DbConfig db) => _db = db;

        // --- 1. 自動削除設定 (午前4時実行用) ---

        [SlashCommand("deleteago", "X日経過したメッセージを午前4時に自動削除する設定")]
        public async Task SetDeleteAgo(
            [Summary("days", "何日前までのメッセージを残すか")] 
            [Choice("1日前", 1), Choice("2日前", 2), Choice("3日前", 3), Choice("7日前", 7)] int days,
            [Summary("protect", "削除から保護する対象")]
            [Choice("なし", 0), Choice("画像あり", 1), Choice("リアクションあり", 2), Choice("画像またはリアクションあり", 3)] int protect)
        {
            using var conn = _db.GetConnection();
            const string sql = @"
                INSERT INTO DeleteConfigs (ChannelId, GuildId, Days, ProtectType) 
                VALUES (@cid, @gid, @d, @p) 
                ON DUPLICATE KEY UPDATE Days = @d, ProtectType = @p";

            await conn.ExecuteAsync(sql, new { cid = Context.Channel.Id, gid = Context.Guild.Id, d = days, p = protect });
            
            string pText = protect switch { 1 => "画像", 2 => "リアクション", 3 => "画像/リアクション", _ => "なし" };
            await RespondAsync($"✅ 設定完了: {days}日以上前のメッセージを毎日午前4時に削除します。(保護: {pText})", ephemeral: true);
        }

        [SlashCommand("deleteago_list", "自動削除設定の一覧表示")]
        public async Task DeleteAgoList()
        {
            using var conn = _db.GetConnection();
            var configs = await conn.QueryAsync("SELECT * FROM DeleteConfigs WHERE GuildId = @gid", new { gid = Context.Guild.Id });

            if (!configs.Any())
            {
                await RespondAsync("自動削除が設定されているチャンネルはありません。", ephemeral: true);
                return;
            }

            var embed = new EmbedBuilder().WithTitle("🗑️ 自動削除設定一覧").WithColor(Color.Red);
            var builder = new ComponentBuilder();

            foreach (var c in configs)
            {
                string pText = (int)c.ProtectType switch { 1 => "画像", 2 => "リアクション", 3 => "画像/リアクション", _ => "なし" };
                embed.AddField($"#{(await Context.Guild.GetChannelAsync((ulong)c.ChannelId))?.Name ?? "不明"}", 
                               $"{c.Days}日前を削除 / 保護: {pText}");
                
                builder.WithButton($"削除: {(ulong)c.ChannelId}", $"delago_rmv_{c.ChannelId}", ButtonStyle.Danger);
            }

            await RespondAsync(embed: embed.Build(), components: builder.Build(), ephemeral: true);
        }

        // --- 2. 右クリック範囲削除 (Context Menu) ---

        [MessageCommand("🚩 開始場所")]
        public async Task SetRangeStart(IMessage msg)
        {
            _deleteStarts[Context.User.Id] = msg.Id;
            await RespondAsync("📍 開始地点を記憶しました。終了したいメッセージで「🚩 終了場所」を選んでください。", ephemeral: true);
        }

        [MessageCommand("🚩 終了場所")]
        public async Task SetRangeEnd(IMessage msg)
        {
            if (!_deleteStarts.TryGetValue(Context.User.Id, out var startId))
            {
                await RespondAsync("❌ 先に「🚩 開始場所」を選択してください。", ephemeral: true);
                return;
            }

            // 保護対象を選択させるメニュー
            var menu = new SelectMenuBuilder()
                .WithCustomId($"range_exec:{startId}:{msg.Id}")
                .WithPlaceholder("保護する対象を選択して削除実行")
                .AddOption("なし（すべて削除）", "0")
                .AddOption("画像を保護", "1")
                .AddOption("リアクションを保護", "2")
                .AddOption("画像とリアクションを保護", "3");

            await RespondAsync("削除範囲の保護ルールを選択してください：", 
                components: new ComponentBuilder().WithSelectMenu(menu).Build(), ephemeral: true);
        }

        // 範囲削除の実行処理 (セレクトメニュー選択時)
        [ComponentInteraction("range_exec:*:*")]
        public async Task ExecuteRangeDelete(string startIdStr, string endIdStr, string[] selectedRoles)
        {
            await DeferAsync(ephemeral: true);
            
            ulong startId = ulong.Parse(startIdStr);
            ulong endId = ulong.Parse(endIdStr);
            int protect = int.Parse(selectedRoles[0]);

            // 開始地点と終了地点のIDを比較して順序を整える
            var minId = Math.Min(startId, endId);
            var maxId = Math.Max(startId, endId);

            // メッセージ取得 (100件制限の簡易実装。100件超える場合はループが必要)
            var messages = await Context.Channel.GetMessagesAsync(minId, Direction.After, 100).FlattenAsync();
            var targetMsgs = messages.Where(m => m.Id <= maxId).ToList();
            
            // 開始地点そのものも含める
            var startMsg = await Context.Channel.GetMessageAsync(minId);
            if (startMsg != null) targetMsgs.Add(startMsg);

            // フィルタリング
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

            if (Context.Channel is ITextChannel ch)
            {
                await ch.DeleteMessagesAsync(toDelete);
                await FollowupAsync($"🗑️ {toDelete.Count}件のメッセージを削除しました。", ephemeral: true);
            }
            
            _deleteStarts.TryRemove(Context.User.Id, out _);
        }

        // deleteago_listからの削除ボタン
        [ComponentInteraction("delago_rmv_*")]
        public async Task RemoveDeleteAgo(string channelId)
        {
            using var conn = _db.GetConnection();
            await conn.ExecuteAsync("DELETE FROM DeleteConfigs WHERE ChannelId = @cid", new { cid = ulong.Parse(channelId) });
            await RespondAsync("✅ 自動削除設定を解除しました。", ephemeral: true);
        }
    }
}

using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Discord_bot.Infrastructure;
using Dapper;
using System.Collections.Concurrent;
using System.Text;

namespace Discord_bot.Module
{
    public class RoleModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly DbConfig _db;
        // セットアップ待機中のユーザーを保持 (UserID, RoleID)
        private static readonly ConcurrentDictionary<ulong, ulong> _pendingSetups = new();

        public RoleModule(DbConfig db) => _db = db;

        [SlashCommand("rolegive", "リアクションロール設定を開始します")]
        public async Task StartRoleGive(
            [Summary("role", "付与・剥奪するロール")] IRole role)
        {
            // 実行したユーザーを待機状態にする
            _pendingSetups[Context.User.Id] = role.Id;

            await RespondAsync(
                $"⚙️ **セットアップ開始**\n" +
                $"1. ロールを紐付けたい**既存のメッセージ**にリアクションしてください。\n" +
                $"2. そのリアクションした絵文字がそのまま登録されます。\n" +
                $"※Botが同じリアクションを付けたら完了です。", 
                ephemeral: true);
        }

        [SlashCommand("rolegive_list", "設定済みのリアクションロール一覧を表示・削除します")]
        public async Task ListRoleGive()
        {
            using var conn = _db.GetConnection();
            const string sql = "SELECT * FROM RoleGiveSettings WHERE GuildId = @gid";
            var settings = (await conn.QueryAsync(sql, new { gid = Context.Guild.Id })).ToList();

            if (!settings.Any())
            {
                await RespondAsync("現在設定されているリアクションロールはありません。", ephemeral: true);
                return;
            }

            var sb = new StringBuilder().AppendLine("【リアクションロール一覧】");
            var builder = new ComponentBuilder();

            foreach (var s in settings)
            {
                sb.AppendLine($"MSG: `{s.MessageId}` | {s.EmojiName} → <@&{s.RoleId}>");
                // ボタンIDにメッセージIDを含めて削除可能にする
                builder.WithButton($"削除: {s.MessageId}", $"rg_del_{s.MessageId}", ButtonStyle.Danger);
            }

            await RespondAsync(sb.ToString(), components: builder.Build(), ephemeral: true);
        }

        // 削除ボタンの処理
        [ComponentInteraction("rg_del_*")]
        public async Task DeleteHandler(string mid)
        {
            using var conn = _db.GetConnection();
            await conn.ExecuteAsync("DELETE FROM RoleGiveSettings WHERE MessageId = @mid", new { mid = ulong.Parse(mid) });
            await RespondAsync($"✅ メッセージ `{mid}` の設定を削除しました。", ephemeral: true);
        }

        // --- イベントハンドラ (InteractionHandlerから呼び出す) ---

        public static async Task HandleReactionAsync(Cacheable<IUserMessage, ulong> cache, SocketReaction reaction, bool isAdded, DbConfig db)
        {
            if (reaction.User.Value.IsBot) return;

            // 1. 新規登録モードの処理
            if (isAdded && _pendingSetups.TryRemove(reaction.UserId, out var roleId))
            {
                using var conn = db.GetConnection();
                const string sql = @"
                    INSERT INTO RoleGiveSettings (MessageId, EmojiName, RoleId, GuildId) 
                    VALUES (@mid, @emo, @rid, @gid) 
                    ON DUPLICATE KEY UPDATE RoleId = @rid, EmojiName = @emo";

                await conn.ExecuteAsync(sql, new
                {
                    mid = reaction.MessageId,
                    emo = reaction.Emote.ToString(),
                    rid = roleId,
                    gid = (reaction.Channel as SocketGuildChannel)?.Guild.Id
                });

                // Bot自身がメッセージに対象のリアクションを付ける (完了の合図)
                var msg = await reaction.Channel.GetMessageAsync(reaction.MessageId) as IUserMessage;
                if (msg != null) await msg.AddReactionAsync(reaction.Emote);

                await reaction.User.Value.SendMessageAsync($"✅ 設定完了: メッセージ `{reaction.MessageId}` に {reaction.Emote} でロールを付与します。");
                return;
            }

            // 2. 通常のロール付与・剥奪
            using (var conn = db.GetConnection())
            {
                const string sql = "SELECT RoleId FROM RoleGiveSettings WHERE MessageId = @mid AND EmojiName = @emo";
                var dbRoleId = await conn.QueryFirstOrDefaultAsync<ulong?>(sql, new { mid = reaction.MessageId, emo = reaction.Emote.ToString() });

                if (dbRoleId.HasValue)
                {
                    var guildUser = (reaction.Channel as SocketGuildChannel)?.Guild.GetUser(reaction.UserId);
                    if (guildUser == null) return;

                    var role = guildUser.Guild.GetRole(dbRoleId.Value);
                    if (role == null) return;

                    if (isAdded) await guildUser.AddRoleAsync(role);
                    else await guildUser.RemoveRoleAsync(role);
                }
            }
        }
    }
}

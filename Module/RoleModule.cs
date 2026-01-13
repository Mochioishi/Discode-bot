using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Discord_bot.Infrastructure;
using Dapper;
using System.Collections.Concurrent;

namespace Discord_bot.Module
{
    public class RoleGiveModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly DbConfig _db;
        private readonly DiscordSocketClient _client;

        // 「誰がどのロールを設定中か」を一時的に保持する辞書
        private static readonly ConcurrentDictionary<ulong, ulong> _pendingSetups = new();

        public RoleGiveModule(DbConfig db, DiscordSocketClient client)
        {
            _db = db;
            _client = client;
        }

        [SlashCommand("rolegive", "リアクションロール設定を開始します")]
        public async Task StartRoleGive(
            [Summary("role", "付与・剥奪するロール")] IRole role)
        {
            // 実行したユーザーIDと、付与したいロールIDを紐付け
            _pendingSetups[Context.User.Id] = role.Id;

            await RespondAsync(
                $"⚙️ **セットアップ開始**\n" +
                $"1. ロールを紐付けたい**既存のメッセージ**にリアクションしてください。\n" +
                $"2. そのリアクションした絵文字がそのまま登録されます。\n" +
                $"※キャンセルする場合は `/rolegive_list` を確認してください。", 
                ephemeral: true);
        }

        // --- 以下、イベント処理ロジック ---

        public static async Task HandleReactionAsync(Cacheable<IUserMessage, ulong> cache, SocketReaction reaction, bool isAdded, DbConfig db)
        {
            if (reaction.User.Value.IsBot) return;

            // 1. セットアップ中（新規登録）かどうかの判定
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

                // 通知を送る（DMまたはチャンネル）
                var user = reaction.User.Value;
                await user.SendMessageAsync($"✅ 設定完了: メッセージ `{reaction.MessageId}` に絵文字 {reaction.Emote} でロールを付与するように設定しました。");
                return;
            }

            // 2. 通常のロール付与・剥奪処理
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

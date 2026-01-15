using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Discord_bot.Infrastructure;
using Dapper;
using System.Collections.Concurrent;
using System.Text;
using System.Linq;
using System.Threading.Tasks;

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
            _pendingSetups[Context.User.Id] = role.Id;

            await RespondAsync(
                $"⚙️ **セットアップ開始**\n" +
                $"1. ロールを紐付けたい**既存のメッセージ**にリアクションしてください。\n" +
                $"2. そのリアクションした絵文字がそのまま登録されます。\n" +
                $"※Botが同じリアクションを付けたら完了です。", 
                ephemeral: true);
        }

        [SlashCommand("rolegive_list", "設定済みのリアクションロール一覧を表示")]
        public async Task ListRoleGive()
        {
            await DeferAsync(ephemeral: true);
            using var conn = _db.GetConnection();
            const string sql = "SELECT * FROM RoleGiveSettings WHERE GuildId = @gid";
            var settings = (await conn.QueryAsync(sql, new { gid = (long)Context.Guild.Id })).ToList();

            if (!settings.Any())
            {
                await FollowupAsync("現在設定されているリアクションロールはありません。", ephemeral: true);
                return;
            }

            var sb = new StringBuilder().AppendLine("【リアクションロール一覧】");
            var builder = new ComponentBuilder();

            foreach (var s in settings)
            {
                // PostgreSQL小文字対策
                var mid = (ulong)(long)s.messageid;
                var rid = (ulong)(long)s.roleid;
                var emo = (string)s.emojiname;

                sb.AppendLine($"MSG: `{mid}` | {emo} → <@&{rid}>");
                builder.WithButton($"削除: {mid}", $"rg_del_{mid}", ButtonStyle.Danger);
            }

            await FollowupAsync(sb.ToString(), components: builder.Build(), ephemeral: true);
        }

        [ComponentInteraction("rg_del_*")]
        public async Task DeleteHandler(string mid)
        {
            await DeferAsync(ephemeral: true);
            using var conn = _db.GetConnection();
            await conn.ExecuteAsync("DELETE FROM RoleGiveSettings WHERE MessageId = @mid", new { mid = long.Parse(mid) });
            await FollowupAsync($"✅ メッセージ `{mid}` の設定を削除しました。", ephemeral: true);
        }

        // --- イベントハンドラ ---

        public static async Task HandleReactionAsync(Cacheable<IUserMessage, ulong> cache, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction, bool isAdded, DbConfig db)
        {
            if (reaction.User.Value.IsBot) return;

            // 1. 新規登録モードの処理
            if (isAdded && _pendingSetups.TryRemove(reaction.UserId, out var roleId))
            {
                using var conn = db.GetConnection();
                const string sql = @"
                    INSERT INTO RoleGiveSettings (MessageId, EmojiName, RoleId, GuildId) 
                    VALUES (@mid, @emo, @rid, @gid) 
                    ON CONFLICT (MessageId) 
                    DO UPDATE SET RoleId = @rid, EmojiName = @emo";

                var gid = (reaction.Channel as SocketGuildChannel)?.Guild.Id;

                await conn.ExecuteAsync(sql, new
                {
                    mid = (long)reaction.MessageId,
                    emo = reaction.Emote.ToString(),
                    rid = (long)roleId,
                    gid = (long?)gid
                });

                var msg = await reaction.Channel.GetMessageAsync(reaction.MessageId) as IUserMessage;
                if (msg != null) await msg.AddReactionAsync(reaction.Emote);
                
                // 設定者に通知（DM送信）
                try { await reaction.User.Value.SendMessageAsync($"✅ 設定完了: メッセージ `{reaction.MessageId}` に {reaction.Emote} でロールを付与します。"); } catch { }
                return;
            }

            // 2. 通常のロール付与・剥奪
            using (var conn = db.GetConnection())
            {
                const string sql = "SELECT roleid FROM RoleGiveSettings WHERE MessageId = @mid AND EmojiName = @emo";
                var result = await conn.QueryFirstOrDefaultAsync(sql, new { mid = (long)reaction.MessageId, emo = reaction.Emote.ToString() });

                if (result != null)
                {
                    ulong dbRoleId = (ulong)(long)result.roleid;
                    var guildUser = (reaction.Channel as SocketGuildChannel)?.Guild.GetUser(reaction.UserId);
                    if (guildUser == null) return;

                    var role = guildUser.Guild.GetRole(dbRoleId);
                    if (role == null) return;

                    if (isAdded) await guildUser.AddRoleAsync(role);
                    else await guildUser.RemoveRoleAsync(role);
                }
            }
        }
    }
}

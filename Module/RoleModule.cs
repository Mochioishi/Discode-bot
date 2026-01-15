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
        // セットアップ待機中のユーザーを保持
        public static readonly ConcurrentDictionary<ulong, ulong> _pendingSetups = new();

        public RoleModule(DbConfig db) => _db = db;

        [SlashCommand("rolegive", "リアクションロール設定を開始します")]
        public async Task StartRoleGive([Summary("role", "付与・剥奪するロール")] IRole role)
        {
            _pendingSetups[Context.User.Id] = role.Id;
            await RespondAsync("⚙️ **セットアップ開始**\n既存のメッセージにリアクションしてください。その絵文字が登録されます。", ephemeral: true);
        }

        [SlashCommand("rolegive_list", "設定済みのリアクションロール一覧を表示")]
        public async Task ListRoleGive()
        {
            await DeferAsync(ephemeral: true);
            using var conn = _db.GetConnection();
            var settings = (await conn.QueryAsync("SELECT * FROM RoleGiveSettings WHERE GuildId = @gid", new { gid = (long)Context.Guild.Id })).ToList();

            if (!settings.Any())
            {
                await FollowupAsync("設定されているリアクションロールはありません。", ephemeral: true);
                return;
            }

            var sb = new StringBuilder().AppendLine("【リアクションロール一覧】");
            var builder = new ComponentBuilder();
            foreach (var s in settings)
            {
                var mid = (ulong)(long)s.messageid;
                var rid = (ulong)(long)s.roleid;
                sb.AppendLine($"MSG: `{mid}` | {s.emojiname} → <@&{rid}>");
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
            await FollowupAsync($"✅ 設定を削除しました。", ephemeral: true);
        }

        // --- イベントハンドラ ---
        public static async Task HandleReactionAsync(Cacheable<IUserMessage, ulong> cache, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction, bool isAdded, DbConfig db)
        {
            if (reaction.User.Value.IsBot) return;

            // 1. 新規登録モード
            if (isAdded && _pendingSetups.TryRemove(reaction.UserId, out var roleId))
            {
                using var conn = db.GetConnection();
                const string sql = @"
                    INSERT INTO RoleGiveSettings (MessageId, EmojiName, RoleId, GuildId) 
                    VALUES (@mid, @emo, @rid, @gid) 
                    ON CONFLICT (MessageId) DO UPDATE SET RoleId = @rid, EmojiName = @emo";

                var gid = (reaction.Channel as SocketGuildChannel)?.Guild.Id;
                await conn.ExecuteAsync(sql, new {
                    mid = (long)reaction.MessageId,
                    emo = reaction.Emote.ToString(),
                    rid = (long)roleId,
                    gid = (long?)gid
                });

                var msg = await reaction.Channel.GetMessageAsync(reaction.MessageId) as IUserMessage;
                if (msg != null) await msg.AddReactionAsync(reaction.Emote);
                return;
            }

            // 2. ロール付与・剥奪
            using var conn2 = db.GetConnection();
            var result = await conn2.QueryFirstOrDefaultAsync("SELECT roleid FROM RoleGiveSettings WHERE MessageId = @mid AND EmojiName = @emo", 
                new { mid = (long)reaction.MessageId, emo = reaction.Emote.ToString() });

            if (result != null)
            {
                var guildUser = (reaction.Channel as SocketGuildChannel)?.Guild.GetUser(reaction.UserId);
                if (guildUser != null)
                {
                    var role = guildUser.Guild.GetRole((ulong)(long)result.roleid);
                    if (role != null)
                    {
                        if (isAdded) await guildUser.AddRoleAsync(role);
                        else await guildUser.RemoveRoleAsync(role);
                    }
                }
            }
        }
    }
}

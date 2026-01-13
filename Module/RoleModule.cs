using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Discord_bot.Infrastructure;
using Dapper;
using System.Collections.Concurrent;
using System.Text;

namespace Discord_bot.Module
{
    public class RoleGiveModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly DbConfig _db;
        private static readonly ConcurrentDictionary<ulong, ulong> _pendingSetups = new();

        public RoleGiveModule(DbConfig db) => _db = db;

        [SlashCommand("rolegive", "リアクションロール設定を開始します")]
        public async Task StartRoleGive([Summary("role", "付与するロール")] IRole role)
        {
            _pendingSetups[Context.User.Id] = role.Id;
            await RespondAsync("⚙️ **セットアップ開始**: ロールを紐付けたい既存のメッセージにリアクションしてください。その絵文字が登録されます。", ephemeral: true);
        }

        [SlashCommand("rolegive_list", "リアクションロールの設定一覧を表示します")]
        public async Task ListRoleGive()
        {
            using var conn = _db.GetConnection();
            var settings = await conn.QueryAsync("SELECT * FROM RoleGiveSettings WHERE GuildId = @gid", new { gid = Context.Guild.Id });

            if (!settings.Any())
            {
                await RespondAsync("設定されているリアクションロールはありません。", ephemeral: true);
                return;
            }

            var sb = new StringBuilder().AppendLine("【リアクションロール一覧】");
            var builder = new ComponentBuilder();

            foreach (var s in settings)
            {
                sb.AppendLine($"MSG ID: `{s.MessageId}` | {s.EmojiName} → <@&{s.RoleId}>");
                builder.WithButton($"削除: {s.MessageId}", $"rg_del_{s.MessageId}", ButtonStyle.Danger);
            }

            await RespondAsync(sb.ToString(), components: builder.Build(), ephemeral: true);
        }

        [ComponentInteraction("rg_del_*")]
        public async Task DeleteRoleGive(string mid)
        {
            using var conn = _db.GetConnection();
            await conn.ExecuteAsync("DELETE FROM RoleGiveSettings WHERE MessageId = @mid", new { mid = ulong.Parse(mid) });
            await RespondAsync("✅ 設定を削除しました。", ephemeral: true);
        }

        public static async Task HandleReactionAsync(Cacheable<IUserMessage, ulong> cache, SocketReaction reaction, bool isAdded, DbConfig db)
        {
            if (reaction.User.Value.IsBot) return;

            // 新規セットアップ
            if (isAdded && _pendingSetups.TryRemove(reaction.UserId, out var roleId))
            {
                using var conn = db.GetConnection();
                const string sql = @"
                    INSERT INTO RoleGiveSettings (MessageId, EmojiName, RoleId, GuildId) 
                    VALUES (@mid, @emo, @rid, @gid) 
                    ON DUPLICATE KEY UPDATE RoleId = @rid, EmojiName = @emo";

                await conn.ExecuteAsync(sql, new {
                    mid = reaction.MessageId, emo = reaction.Emote.ToString(), rid = roleId, gid = (reaction.Channel as SocketGuildChannel)?.Guild.Id
                });

                // Botが該当メッセージにリアクションして完了 (要件)
                var msg = await reaction.Channel.GetMessageAsync(reaction.MessageId) as IUserMessage;
                if (msg != null) await msg.AddReactionAsync(reaction.Emote);

                await reaction.User.Value.SendMessageAsync($"✅ メッセージ `{reaction.MessageId}` にロールを紐付けました。");
                return;
            }

            // 通常の付与・剥奪
            using (var conn = db.GetConnection())
            {
                const string sql = "SELECT RoleId FROM RoleGiveSettings WHERE MessageId = @mid AND EmojiName = @emo";
                var dbRoleId = await conn.QueryFirstOrDefaultAsync<ulong?>(sql, new { mid = reaction.MessageId, emo = reaction.Emote.ToString() });

                if (dbRoleId.HasValue)
                {
                    var user = (reaction.Channel as SocketGuildChannel)?.Guild.GetUser(reaction.UserId);
                    if (isAdded) await user?.AddRoleAsync(dbRoleId.Value);
                    else await user?.RemoveRoleAsync(dbRoleId.Value);
                }
            }
        }
    }
}

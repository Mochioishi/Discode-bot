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
        
        public static readonly ConcurrentDictionary<ulong, (ulong RoleId, IInteractionContext Context)> _pendingSetups = new();

        public RoleModule(DbConfig db) => _db = db;

        [SlashCommand("rolegive", "リアクションロール設定を開始します")]
        public async Task StartRoleGive([Summary("role", "付与・剥奪するロール")] IRole role)
        {
            _pendingSetups[Context.User.Id] = (role.Id, Context);

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
            
            var settings = (await conn.QueryAsync("SELECT * FROM RoleGiveSettings WHERE GuildId = @gid", new { gid = (long)Context.Guild.Id })).ToList();

            if (!settings.Any())
            {
                await FollowupAsync("現在設定されているリアクションロールはありません。", ephemeral: true);
                return;
            }

            var embed = new EmbedBuilder().WithTitle("🎭 リアクションロール一覧").WithColor(Color.Blue);
            var builder = new ComponentBuilder();

            foreach (var s in settings)
            {
                var mid = (ulong)(long)s.messageid;
                var rid = (ulong)(long)s.roleid;
                var cid = (ulong)(long)(s.channelid ?? 0);
                var emo = (string)s.emojiname;

                var channel = Context.Guild.GetChannel(cid);
                string channelName = channel?.Name ?? "不明なch";
                var role = Context.Guild.GetRole(rid);
                string roleMention = role?.Mention ?? "不明なロール";

                embed.AddField($"#{channelName}", $"{emo} → {roleMention}");
                builder.WithButton($"設定削除: #{channelName}", $"rg_del_{mid}", ButtonStyle.Danger);
            }

            await FollowupAsync(embed: embed.Build(), components: builder.Build(), ephemeral: true);
        }

        [ComponentInteraction("rg_del_*")]
        public async Task DeleteHandler(string mid)
        {
            await DeferAsync(ephemeral: true);
            
            using var conn = _db.GetConnection();
            long messageId = long.Parse(mid);

            // 1. 削除前にDBから設定情報を取得（リアクションを外すため）
            var setting = await conn.QueryFirstOrDefaultAsync(
                "SELECT channelid, emojiname FROM RoleGiveSettings WHERE MessageId = @mid", 
                new { mid = messageId });

            if (setting != null)
            {
                try
                {
                    ulong cId = (ulong)(long)setting.channelid;
                    string emojiStr = setting.emojiname;

                    // チャンネルとメッセージを取得してリアクションを外す
                    var channel = await Context.Guild.GetChannelAsync(cId) as IMessageChannel;
                    if (channel != null)
                    {
                        var msg = await channel.GetMessageAsync(ulong.Parse(mid)) as IUserMessage;
                        if (msg != null)
                        {
                            IEmote emote;
                            // カスタム絵文字か標準絵文字かを判定してパース
                            if (Emote.TryParse(emojiStr, out var customEmote)) emote = customEmote;
                            else emote = new Emoji(emojiStr);

                            await msg.RemoveReactionAsync(emote, Context.Client.CurrentUser);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // メッセージが既に消えている場合などは無視
                    Console.WriteLine($"[RoleGive Delete Info] リアクション解除スキップ: {ex.Message}");
                }
            }

            // 2. DBから設定を削除
            await conn.ExecuteAsync("DELETE FROM RoleGiveSettings WHERE MessageId = @mid", new { mid = messageId });
            
            await FollowupAsync($"✅ 設定を解除し、Botのリアクションを削除しました。", ephemeral: true);
        }

        public static async Task HandleReactionAsync(Cacheable<IUserMessage, ulong> cache, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction, bool isAdded, DbConfig db)
        {
            if (reaction.User.Value.IsBot) return;

            if (isAdded && _pendingSetups.TryRemove(reaction.UserId, out var setup))
            {
                using var conn = db.GetConnection();
                const string sql = @"
                    INSERT INTO RoleGiveSettings (MessageId, EmojiName, RoleId, GuildId, ChannelId) 
                    VALUES (@mid, @emo, @rid, @gid, @chid) 
                    ON CONFLICT (MessageId) 
                    DO UPDATE SET RoleId = @rid, EmojiName = @emo, ChannelId = @chid";

                var socketChannel = reaction.Channel as SocketGuildChannel;
                await conn.ExecuteAsync(sql, new {
                    mid = (long)reaction.MessageId,
                    emo = reaction.Emote.ToString(),
                    rid = (long)setup.RoleId,
                    gid = (long?)socketChannel?.Guild.Id,
                    chid = (long)reaction.Channel.Id
                });

                var msg = await reaction.Channel.GetMessageAsync(reaction.MessageId) as IUserMessage;
                if (msg != null) await msg.AddReactionAsync(reaction.Emote);

                var role = socketChannel?.Guild.GetRole(setup.RoleId);
                try {
                    await setup.Context.Interaction.ModifyOriginalResponseAsync(prop => 
                        prop.Content = $"✅ 設定しました： {reaction.Emote} → {role?.Mention ?? "不明なロール"}");
                } catch { }
                return;
            }

            using (var conn = db.GetConnection())
            {
                const string sql = "SELECT roleid FROM RoleGiveSettings WHERE MessageId = @mid AND EmojiName = @emo";
                var result = await conn.QueryFirstOrDefaultAsync(sql, new { mid = (long)reaction.MessageId, emo = reaction.Emote.ToString() });

                if (result != null)
                {
                    var guildUser = (reaction.Channel as SocketGuildChannel)?.Guild.GetUser(reaction.UserId);
                    if (guildUser == null) return;

                    var role = guildUser.Guild.GetRole((ulong)(long)result.roleid);
                    if (role == null) return;

                    if (isAdded) await guildUser.AddRoleAsync(role);
                    else await guildUser.RemoveRoleAsync(role);
                }
            }
        }
    }
}

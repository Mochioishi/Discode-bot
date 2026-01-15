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
        // セットアップ待機中のユーザーと、その時のInteractionContextを保持
        // (UserID, (RoleID, Context))
        private static readonly ConcurrentDictionary<ulong, (ulong RoleId, IInteractionContext Context)> _pendingSetups = new();

        public RoleModule(DbConfig db) => _db = db;

        [SlashCommand("rolegive", "リアクションロール設定を開始します")]
        public async Task StartRoleGive([Summary("role", "付与・剥奪するロール")] IRole role)
        {
            // 後でメッセージを書き換えるためにContextを保存
            _pendingSetups[Context.User.Id] = (role.Id, Context);
            await RespondAsync("⚙️ **セットアップ開始**\n既存のメッセージにリアクションしてください。その絵文字が登録されます。", ephemeral: true);
        }

        [SlashCommand("rolegive_list", "設定済みのリアクションロール一覧を表示")]
        public async Task ListRoleGive()
        {
            await DeferAsync(ephemeral: true);
            using var conn = _db.GetConnection();
            // Guild内の全設定を取得
            var settings = (await conn.QueryAsync("SELECT * FROM RoleGiveSettings WHERE GuildId = @gid", new { gid = (long)Context.Guild.Id })).ToList();

            if (!settings.Any())
            {
                await FollowupAsync("設定されているリアクションロールはありません。", ephemeral: true);
                return;
            }

            var embed = new EmbedBuilder().WithTitle("🎭 リアクションロール設定一覧").WithColor(Color.Blue);
            var builder = new ComponentBuilder();

            foreach (var s in settings)
            {
                var mid = (ulong)(long)s.messageid;
                var rid = (ulong)(long)s.roleid;
                var emo = (string)s.emojiname;

                // チャンネル名とロール名を取得
                var role = Context.Guild.GetRole(rid);
                var msg = await Context.Channel.GetMessageAsync(mid); // 簡易的に現在のchから探すが、見つからない場合はIDを表示
                var channel = Context.Guild.Channels.FirstOrDefault(c => c.Id == (ulong)(long)s.channelid); // DBにChannelIdがある場合
                
                // ※もしDBにChannelIdを保存していない場合は、メッセージオブジェクトから逆引き
                string channelName = "不明なch";
                if (msg != null) channelName = msg.Channel.Name;

                embed.AddField($"#{channelName}", $"{emo} → <@&{rid}>");
                builder.WithButton($"設定削除: #{channelName}", $"rg_del_{mid}", ButtonStyle.Danger);
            }

            await FollowupAsync(embed: embed.Build(), components: builder.Build(), ephemeral: true);
        }

        [ComponentInteraction("rg_del_*")]
        public async Task DeleteHandler(string mid)
        {
            await DeferAsync(ephemeral: true);
            using var conn = _db.GetConnection();
            await conn.ExecuteAsync("DELETE FROM RoleGiveSettings WHERE MessageId = @mid", new { mid = long.Parse(mid) });
            await FollowupAsync($"✅ 指定したメッセージのリアクションロール設定を解除しました。", ephemeral: true);
        }

        public static async Task HandleReactionAsync(Cacheable<IUserMessage, ulong> cache, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction, bool isAdded, DbConfig db)
        {
            if (reaction.User.Value.IsBot) return;

            // 1. 新規登録モード
            if (isAdded && _pendingSetups.TryRemove(reaction.UserId, out var setup))
            {
                using var conn = db.GetConnection();
                const string sql = @"
                    INSERT INTO RoleGiveSettings (MessageId, EmojiName, RoleId, GuildId, ChannelId) 
                    VALUES (@mid, @emo, @rid, @gid, @chid) 
                    ON CONFLICT (MessageId) DO UPDATE SET RoleId = @rid, EmojiName = @emo";

                var socketChannel = reaction.Channel as SocketGuildChannel;
                var gid = socketChannel?.Guild.Id;

                await conn.ExecuteAsync(sql, new {
                    mid = (long)reaction.MessageId,
                    emo = reaction.Emote.ToString(),
                    rid = (long)setup.RoleId,
                    gid = (long?)gid,
                    chid = (long)reaction.Channel.Id
                });

                // Botがリアクションを付けて完了通知
                var msg = await reaction.Channel.GetMessageAsync(reaction.MessageId) as IUserMessage;
                if (msg != null) await msg.AddReactionAsync(reaction.Emote);

                // --- 元のSlashコマンドの応答を書き換え ---
                var role = socketChannel?.Guild.GetRole(setup.RoleId);
                string roleName = role?.Mention ?? "不明なロール";
                await setup.Context.Interaction.ModifyOriginalResponseAsync(prop => 
                    prop.Content = $"✅ 設定しました： {reaction.Emote} → {roleName}");
                
                return;
            }

            // 2. ロール付与・剥奪（中略：以前のコードと同じ）
            // ... (ここに以前の付与/剥奪ロジックを記述)
        }
    }
}

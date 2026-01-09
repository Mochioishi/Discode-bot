using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Infrastructure;
using Npgsql;
using System;
using System.Threading.Tasks;

namespace DiscordBot.Modules
{
    public class RoleModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly DiscordSocketClient _client;
        private readonly string _conn;

        public RoleModule(DiscordSocketClient client)
        {
            _client = client;
            _conn = DbConfig.GetConnectionString();

            // リアクションの監視イベントを登録
            _client.ReactionAdded += OnReactionAdded;
        }

        [SlashCommand("rolegive", "リアクションでロールを付与するメッセージを作成します")]
        public async Task RoleGive(
            [Summary("text", "メッセージ本文")] string text,
            [Summary("role", "付与するロール")] IRole role,
            [Summary("emoji", "使用する絵文字")] string emoji,
            [Summary("minutes", "自動削除までの分計 (0で永続)")] int minutes = 0)
        {
            // 1. メッセージを送信
            var embed = new EmbedBuilder()
                .WithDescription(text)
                .WithFooter(f => f.Text = $"リアクション {emoji} で @{role.Name} を付与します")
                .WithColor(Color.Green)
                .Build();

            // 返信ではなくチャンネルに新規投稿
            await RespondAsync("作成中...", ephemeral: true);
            var msg = await Context.Channel.SendMessageAsync(embed: embed);

            // 2. 絵文字をボットが自動で付ける
            if (Emoji.TryParse(emoji, out var resultEmoji))
            {
                await msg.AddReactionAsync(resultEmoji);
            }
            else if (Emote.TryParse(emoji, out var resultEmote))
            {
                await msg.AddReactionAsync(resultEmote);
            }

            // 3. DBにリアクションロール設定を保存
            using var conn = new NpgsqlConnection(_conn);
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand(
                "INSERT INTO \"ReactionRoles\" (\"MessageId\", \"Emoji\", \"RoleId\") VALUES (@mid, @emo, @rid)", conn);
            cmd.Parameters.AddWithValue("mid", (long)msg.Id);
            cmd.Parameters.AddWithValue("emo", emoji);
            cmd.Parameters.AddWithValue("rid", (long)role.Id);
            await cmd.ExecuteNonQueryAsync();

            // 4. 自動削除の予約 (minutes > 0 の場合)
            if (minutes > 0)
            {
                var deleteAt = DateTimeOffset.Now.AddMinutes(minutes);
                using var delCmd = new NpgsqlCommand(
                    "INSERT INTO \"ScheduledDeletions\" (\"MessageId\", \"ChannelId\", \"DeleteAt\") VALUES (@mid, @cid, @at)", conn);
                delCmd.Parameters.AddWithValue("mid", (long)msg.Id);
                delCmd.Parameters.AddWithValue("cid", (long)msg.Channel.Id);
                delCmd.Parameters.AddWithValue("at", deleteAt);
                await delCmd.ExecuteNonQueryAsync();
            }

            await FollowupAsync("✅ リアクションロールを作成しました。", ephemeral: true);
        }

        // --- リアクション検知ロジック ---
        private async Task OnReactionAdded(Cacheable<IUserMessage, ulong> cache, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
        {
            if (reaction.User.Value.IsBot) return;

            try
            {
                using var conn = new NpgsqlConnection(_conn);
                await conn.OpenAsync();

                using var cmd = new NpgsqlCommand(
                    "SELECT \"RoleId\" FROM \"ReactionRoles\" WHERE \"MessageId\" = @mid AND \"Emoji\" = @emo", conn);
                cmd.Parameters.AddWithValue("mid", (long)reaction.MessageId);
                cmd.Parameters.AddWithValue("emo", reaction.Emote.Name);

                var result = await cmd.ExecuteScalarAsync();
                if (result != null)
                {
                    var roleId = (ulong)(long)result;
                    var guildUser = reaction.User.Value as IGuildUser;
                    var role = guildUser?.Guild.GetRole(roleId);

                    if (guildUser != null && role != null)
                    {
                        await guildUser.AddRoleAsync(role);
                        // 通知が必要ならここに追記（例：DM送信など）
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Role Error]: {ex.Message}");
            }
        }
    }
}

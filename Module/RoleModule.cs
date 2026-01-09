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
            
            // 以前のコード同様、追加と削除の両方を監視
            _client.ReactionAdded += OnReactionAdded;
            _client.ReactionRemoved += OnReactionRemoved;
        }

        [SlashCommand("rolegive", "リアクションでロールを付与")]
        public async Task RoleGive(string text, IRole role, string emoji, int minutes = 0)
        {
            var embed = new EmbedBuilder()
                .WithDescription(text)
                .WithFooter(f => f.Text = $"リアクション {emoji} で @{role.Name} を付与")
                .WithColor(Color.Green).Build();

            await RespondAsync("作成中...", ephemeral: true);
            var msg = await Context.Channel.SendMessageAsync(embed: embed);

            // 絵文字を付ける (以前のロジック)
            if (Emoji.TryParse(emoji, out var e1)) await msg.AddReactionAsync(e1);
            else if (Emote.TryParse(emoji, out var e2)) await msg.AddReactionAsync(e2);

            using var conn = new NpgsqlConnection(_conn);
            await conn.OpenAsync();
            
            // ReactionRolesへの保存
            using var cmd = new NpgsqlCommand("INSERT INTO \"ReactionRoles\" (\"MessageId\", \"Emoji\", \"RoleId\") VALUES (@mid, @emo, @rid)", conn);
            cmd.Parameters.AddWithValue("mid", (long)msg.Id);
            cmd.Parameters.AddWithValue("emo", emoji);
            cmd.Parameters.AddWithValue("rid", (long)role.Id);
            await cmd.ExecuteNonQueryAsync();

            // 予約削除の連携
            if (minutes > 0)
            {
                using var delCmd = new NpgsqlCommand("INSERT INTO \"ScheduledDeletions\" (\"MessageId\", \"ChannelId\", \"DeleteAt\") VALUES (@mid, @cid, @at)", conn);
                delCmd.Parameters.AddWithValue("mid", (long)msg.Id);
                delCmd.Parameters.AddWithValue("cid", (long)msg.Channel.Id);
                delCmd.Parameters.AddWithValue("at", DateTimeOffset.Now.AddMinutes(minutes));
                await delCmd.ExecuteNonQueryAsync();
            }
            await FollowupAsync("✅ 作成完了", ephemeral: true);
        }

        private async Task OnReactionAdded(Cacheable<IUserMessage, ulong> cache, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
            => await HandleRoleAsync(reaction, true);

        private async Task OnReactionRemoved(Cacheable<IUserMessage, ulong> cache, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
            => await HandleRoleAsync(reaction, false);

        private async Task HandleRoleAsync(SocketReaction reaction, bool add)
        {
            if (reaction.User.Value.IsBot) return;
            try
            {
                using var conn = new NpgsqlConnection(_conn);
                await conn.OpenAsync();
                
                // 以前のコードに基づき、絵文字名またはフルテキストで照合
                using var cmd = new NpgsqlCommand("SELECT \"RoleId\" FROM \"ReactionRoles\" WHERE \"MessageId\" = @mid AND (\"Emoji\" = @emo OR \"Emoji\" LIKE '%' || @name || '%')", conn);
                cmd.Parameters.AddWithValue("mid", (long)reaction.MessageId);
                cmd.Parameters.AddWithValue("emo", reaction.Emote.ToString());
                cmd.Parameters.AddWithValue("name", reaction.Emote.Name);

                var result = await cmd.ExecuteScalarAsync();
                if (result != null)
                {
                    var guildUser = reaction.User.Value as IGuildUser;
                    var role = guildUser?.Guild.GetRole((ulong)(long)result);
                    if (guildUser != null && role != null)
                    {
                        if (add) await guildUser.AddRoleAsync(role);
                        else await guildUser.RemoveRoleAsync(role);
                    }
                }
            } catch (Exception ex) { Console.WriteLine(ex.Message); }
        }
    }
}

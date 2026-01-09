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
            _client.ReactionAdded += OnReactionAdded;
            _client.ReactionRemoved += OnReactionRemoved;
        }

        [SlashCommand("rolegive", "リアクションでロール付与設定")]
        public async Task RoleGive(string text, IRole role, string emoji)
        {
            var eb = new EmbedBuilder().WithDescription(text).WithFooter($"リアクション {emoji} で @{role.Name} を付与").WithColor(Color.Green).Build();
            await RespondAsync("作成中...", ephemeral: true);
            var msg = await Context.Channel.SendMessageAsync(embed: eb);
            
            if (Emoji.TryParse(emoji, out var e1)) await msg.AddReactionAsync(e1);
            else if (Emote.TryParse(emoji, out var e2)) await msg.AddReactionAsync(e2);

            using var conn = new NpgsqlConnection(_conn); await conn.OpenAsync();
            using var cmd = new NpgsqlCommand(@"INSERT INTO ""ReactionRoles"" (""MessageId"", ""Emoji"", ""RoleId"") VALUES (@mid, @emo, @rid)", conn);
            cmd.Parameters.AddWithValue("mid", (long)msg.Id);
            cmd.Parameters.AddWithValue("emo", emoji);
            cmd.Parameters.AddWithValue("rid", (long)role.Id);
            await cmd.ExecuteNonQueryAsync();
            await FollowupAsync("✅ 作成完了", ephemeral: true);
        }

        private async Task OnReactionAdded(Cacheable<IUserMessage, ulong> c, Cacheable<IMessageChannel, ulong> ch, SocketReaction r) => await Handle(r, true);
        private async Task OnReactionRemoved(Cacheable<IUserMessage, ulong> c, Cacheable<IMessageChannel, ulong> ch, SocketReaction r) => await Handle(r, false);

        private async Task Handle(SocketReaction r, bool add)
        {
            if (r.User.Value.IsBot) return;
            using var conn = new NpgsqlConnection(_conn); await conn.OpenAsync();
            using var cmd = new NpgsqlCommand(@"SELECT ""RoleId"" FROM ""ReactionRoles"" WHERE ""MessageId"" = @mid AND ""Emoji"" = @emo", conn);
            cmd.Parameters.AddWithValue("mid", (long)r.MessageId);
            cmd.Parameters.AddWithValue("emo", r.Emote.ToString());
            var res = await cmd.ExecuteScalarAsync();
            if (res != null)
            {
                var user = r.User.Value as IGuildUser;
                var role = user?.Guild.GetRole((ulong)(long)res);
                if (user != null && role != null) { if (add) await user.AddRoleAsync(role); else await user.RemoveRoleAsync(role); }
            }
        }
    }
}

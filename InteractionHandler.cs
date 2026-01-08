using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Modules;
using Npgsql;
using System;
using System.Reflection;
using System.Threading.Tasks;

namespace DiscordBot.Services
{
    public class InteractionHandler
    {
        private readonly DiscordSocketClient _client;
        private readonly InteractionService _handler;
        private readonly IServiceProvider _services;
        private readonly string _connectionString;

        public InteractionHandler(DiscordSocketClient client, InteractionService handler, IServiceProvider services)
        {
            _client = client;
            _handler = handler;
            _services = services;
            _connectionString = DbConfig.GetConnectionString();

            _client.InteractionCreated += HandleInteraction;
            _client.ReactionAdded += HandleReactionAdded;
            _client.ReactionRemoved += HandleReactionRemoved;
        }

        public async Task InitializeAsync()
        {
            await _handler.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
        }

        private async Task HandleInteraction(SocketInteraction interaction)
        {
            try
            {
                var context = new SocketInteractionContext(_client, interaction);
                await _handler.ExecuteCommandAsync(context, _services);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        private async Task HandleReactionAdded(Cacheable<IUserMessage, ulong> cachedMessage, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
        {
            if (reaction.User.Value.IsBot) return;

            // --- 1. リアクションロールの設定モード中かチェック ---
            if (RoleModule.PendingSettings.TryRemove(reaction.UserId, out var role))
            {
                try
                {
                    using var conn = new NpgsqlConnection(_connectionString);
                    await conn.OpenAsync();
                    using var cmd = new NpgsqlCommand(
                        "INSERT INTO ReactionRoles (MessageId, Emoji, RoleId) VALUES (@mid, @emoji, @rid) " +
                        "ON CONFLICT (MessageId, Emoji) DO UPDATE SET RoleId = @rid", conn);

                    cmd.Parameters.AddWithValue("mid", (long)reaction.MessageId);
                    cmd.Parameters.AddWithValue("emoji", reaction.Emote.ToString() ?? "");
                    cmd.Parameters.AddWithValue("rid", (long)role.Id);
                    await cmd.ExecuteNonQueryAsync();

                    // ボットもリアクションして設定完了を通知
                    var msg = await reaction.Channel.GetMessageAsync(reaction.MessageId);
                    if (msg is IUserMessage userMsg)
                    {
                        await userMsg.AddReactionAsync(reaction.Emote);
                    }
                    return; // 設定処理が終わったのでここで終了
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DB Error] {ex.Message}");
                }
            }

            // --- 2. 通常のロール付与ロジック ---
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();
                using var cmd = new NpgsqlCommand("SELECT RoleId FROM ReactionRoles WHERE MessageId = @mid AND Emoji = @emoji", conn);
                cmd.Parameters.AddWithValue("mid", (long)reaction.MessageId);
                cmd.Parameters.AddWithValue("emoji", reaction.Emote.ToString() ?? "");

                var result = await cmd.ExecuteScalarAsync();
                if (result != null)
                {
                    var roleId = (ulong)(long)result;
                    var guild = (reaction.Channel as SocketGuildChannel)?.Guild;
                    var user = guild?.GetUser(reaction.UserId);
                    var targetRole = guild?.GetRole(roleId);

                    if (user != null && targetRole != null)
                    {
                        await user.AddRoleAsync(targetRole);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        private async Task HandleReactionRemoved(Cacheable<IUserMessage, ulong> cachedMessage, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
        {
            if (reaction.User.Value.IsBot) return;

            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();
                using var cmd = new NpgsqlCommand("SELECT RoleId FROM ReactionRoles WHERE MessageId = @mid AND Emoji = @emoji", conn);
                cmd.Parameters.AddWithValue("mid", (long)reaction.MessageId);
                cmd.Parameters.AddWithValue("emoji", reaction.Emote.ToString() ?? "");

                var result = await cmd.ExecuteScalarAsync();
                if (result != null)
                {
                    var roleId = (ulong)(long)result;
                    var guild = (reaction.Channel as SocketGuildChannel)?.Guild;
                    var user = guild?.GetUser(reaction.UserId);
                    var targetRole = guild?.GetRole(roleId);

                    if (user != null && targetRole != null)
                    {
                        await user.RemoveRoleAsync(targetRole);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
    }
}

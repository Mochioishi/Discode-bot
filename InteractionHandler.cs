using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Discord_bot.Infrastructure;
using Discord_bot.Module;
using System.Reflection;

namespace Discord_bot
{
    public class InteractionHandler
    {
        private readonly DiscordSocketClient _client;
        private readonly InteractionService _interactionService;
        private readonly IServiceProvider _services;
        private readonly DbConfig _db;

        public InteractionHandler(DiscordSocketClient client, InteractionService interactionService, IServiceProvider services, DbConfig db)
        {
            _client = client;
            _interactionService = interactionService;
            _services = services;
            _db = db;
        }

        public async Task InitializeAsync()
        {
            await _interactionService.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
            _client.InteractionCreated += HandleInteraction;
            _client.Ready += ReadyAsync;
            _client.MessageReceived += HandleMessageAsync;

            // --- 引数を (cache, channel, reaction) の3つに修正 ---
            _client.ReactionAdded += (cache, channel, reaction) => RoleModule.HandleReactionAsync(cache, channel, reaction, true, _db);
            _client.ReactionRemoved += (cache, channel, reaction) => RoleModule.HandleReactionAsync(cache, channel, reaction, false, _db);
        }

        private async Task HandleMessageAsync(SocketMessage msg)
        {
            if (msg is not SocketUserMessage userMsg || userMsg.Author.IsBot) return;
            await PrskModule.HandleMessageAsync(userMsg, _db, _client);
        }

        private async Task ReadyAsync()
        {
            var serverIdStr = Environment.GetEnvironmentVariable("SERVER_ID");
            if (ulong.TryParse(serverIdStr, out ulong guildId))
            {
                await _interactionService.RegisterCommandsToGuildAsync(guildId);
                Console.WriteLine($"[Ready] Registered to guild: {guildId}");
            }
        }

        private async Task HandleInteraction(SocketInteraction interaction)
        {
            try
            {
                var context = new SocketInteractionContext(_client, interaction);
                await _interactionService.ExecuteCommandAsync(context, _services);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Interaction Error] {ex}");
            }
        }
    }
}

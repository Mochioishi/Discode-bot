using System.Reflection;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Discord_bot.Module;
using Discord_bot.Infrastructure;
using Microsoft.Extensions.Configuration;

namespace Discord_bot
{
    public class InteractionHandler
    {
        private readonly DiscordSocketClient _client;
        private readonly InteractionService _commands;
        private readonly IServiceProvider _services;
        private readonly DbConfig _db;
        private readonly IConfiguration _config;

        public InteractionHandler(DiscordSocketClient client, InteractionService commands, IServiceProvider services, DbConfig db, IConfiguration configuration)
        {
            _client = client;
            _commands = commands;
            _services = services;
            _db = db;
            _config = configuration;
        }

        public async Task InitializeAsync()
        {
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);

            _client.InteractionCreated += HandleInteraction;
            _client.Ready += OnReadyAsync;

            // プロセカ監視・リアクションロールのイベントを静的メソッドへ飛ばす
            _client.MessageReceived += (msg) => PrskModule.HandleMessageAsync(msg, _db, _client);
            
            // 【修正箇所】RoleGiveModule ではなく RoleModule を使用
            _client.ReactionAdded += (c, ch, r) => RoleModule.HandleReactionAsync(c, r, true, _db);
            _client.ReactionRemoved += (c, ch, r) => RoleModule.HandleReactionAsync(c, r, false, _db);
        }

        private async Task OnReadyAsync()
        {
            // 重複削除のため deleteMissing: true を使用
            if (ulong.TryParse(_config["SERVER_ID"], out var guildId))
            {
                await _commands.RegisterCommandsToGuildAsync(guildId, deleteMissing: true);
                Console.WriteLine($"[Ready] Registered to guild: {guildId}");
            }

            await _commands.RegisterCommandsGloballyAsync(deleteMissing: true);
            Console.WriteLine("[Ready] Global commands synced.");
        }

        private async Task HandleInteraction(SocketInteraction interaction)
        {
            try
            {
                var context = new SocketInteractionContext(_client, interaction);
                await _commands.ExecuteCommandAsync(context, _services);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Interaction Error] {ex}");
            }
        }
    }
}

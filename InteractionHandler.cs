using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using System.Reflection;

namespace Discord_bot
{
    public class InteractionHandler
    {
        private readonly DiscordSocketClient _client;
        private readonly InteractionService _interactionService;
        private readonly IServiceProvider _services;

        public InteractionHandler(DiscordSocketClient client, InteractionService interactionService, IServiceProvider services)
        {
            _client = client;
            _interactionService = interactionService;
            _services = services;
        }

        public async Task InitializeAsync()
        {
            await _interactionService.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
            _client.InteractionCreated += HandleInteraction;
            _client.Ready += ReadyAsync;
        }

        private async Task ReadyAsync()
        {
            var serverIdStr = Environment.GetEnvironmentVariable("SERVER_ID");
            
            if (ulong.TryParse(serverIdStr, out ulong guildId))
            {
                // --- 重複削除：以前のグローバル登録を一度クリアする ---
                // 重複が完全に消えたら、将来的にこの行はコメントアウトしてもOKです
                // await _client.Rest.DeleteAllGlobalCommandsAsync();
                // Console.WriteLine("[Ready] Global commands cleared to fix duplication.");

                // ギルド（サーバー）限定で登録（即時反映）
                await _interactionService.RegisterCommandsToGuildAsync(guildId);
                Console.WriteLine($"[Ready] Registered to guild: {guildId}");

                // --- デバッグ：登録されたコマンドをログに表示 ---
                Console.WriteLine("=== Registered Commands List ===");
                foreach (var module in _interactionService.Modules)
                {
                    foreach (var cmd in module.SlashCommands)
                    {
                        Console.WriteLine($"[Command] /{cmd.Name} - {cmd.Description}");
                    }
                }
                Console.WriteLine("================================");
            }
            else
            {
                Console.WriteLine("[Ready Warning] SERVER_ID is not set. Commands not registered.");
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

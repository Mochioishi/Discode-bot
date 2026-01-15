using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Discord_bot.Infrastructure; // 追加
using Discord_bot.Module;         // 追加
using System.Reflection;

namespace Discord_bot
{
    public class InteractionHandler
    {
        private readonly DiscordSocketClient _client;
        private readonly InteractionService _interactionService;
        private readonly IServiceProvider _services;
        private readonly DbConfig _db; // 追加

        public InteractionHandler(DiscordSocketClient client, InteractionService interactionService, IServiceProvider services, DbConfig db)
        {
            _client = client;
            _interactionService = interactionService;
            _services = services;
            _db = db; // 追加
        }

        public async Task InitializeAsync()
        {
            await _interactionService.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
            _client.InteractionCreated += HandleInteraction;
            _client.Ready += ReadyAsync;
            
            // --- 追加: メッセージ受信イベントを購読 ---
            _client.MessageReceived += HandleMessageAsync;
        }

        // --- 追加: メッセージ受信時の処理 ---
        private async Task HandleMessageAsync(SocketMessage msg)
        {
            if (msg is not SocketUserMessage userMsg || userMsg.Author.IsBot) return;
            
            // PrskModuleの監視ロジックを呼び出す
            await PrskModule.HandleMessageAsync(userMsg, _db, _client);
        }

        private async Task ReadyAsync()
        {
            var serverIdStr = Environment.GetEnvironmentVariable("SERVER_ID");
            
            if (ulong.TryParse(serverIdStr, out ulong guildId))
            {
                // 重複削除（必要に応じてコメントアウト）
                await _client.Rest.DeleteAllGlobalCommandsAsync();
                
                await _interactionService.RegisterCommandsToGuildAsync(guildId);
                Console.WriteLine($"[Ready] Registered to guild: {guildId}");

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

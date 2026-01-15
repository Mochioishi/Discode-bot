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

            // Readyイベントでコマンドを同期
            _client.Ready += ReadyAsync;
        }

        private async Task ReadyAsync()
        {
            // 環境変数 SERVER_ID を取得
            var serverIdStr = Environment.GetEnvironmentVariable("SERVER_ID");
            
            if (ulong.TryParse(serverIdStr, out ulong guildId))
            {
                // 1. 指定されたギルドにのみコマンドを登録（即時反映される）
                await _interactionService.RegisterCommandsToGuildAsync(guildId);
                Console.WriteLine($"[Ready] Registered to guild: {guildId}");

                // 2. もし以前にグローバル登録してしまったものを消したい場合は、
                // 一時的に以下を有効にして実行するとクリーンアップされます
                // await _client.Rest.DeleteAllGlobalCommandsAsync();
            }
            else
            {
                Console.WriteLine("[Ready Warning] SERVER_ID is not set or invalid. Commands not registered.");
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

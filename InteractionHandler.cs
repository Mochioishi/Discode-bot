using System.Reflection;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Discord_bot.Module; // モジュールの参照
using Discord_bot.Infrastructure;

namespace Discord_bot
{
    public class InteractionHandler
    {
        private readonly DiscordSocketClient _client;
        private readonly InteractionService _commands;
        private readonly IServiceProvider _services;
        private readonly DbConfig _db;

        public InteractionHandler(DiscordSocketClient client, InteractionService commands, IServiceProvider services, DbConfig db)
        {
            _client = client;
            _commands = commands;
            _services = services;
            _db = db;
        }

        public async Task InitializeAsync()
        {
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);

            // イベントの紐付け
            _client.InteractionCreated += HandleInteraction;
            _client.Ready += OnReadyAsync;

            // プロセカ監視用
            _client.MessageReceived += (msg) => PrskModule.HandleMessageAsync(msg, _db, _client);

            // リアクションロール用
            _client.ReactionAdded += (c, ch, r) => RoleGiveModule.HandleReactionAsync(c, r, true, _db);
            _client.ReactionRemoved += (c, ch, r) => RoleGiveModule.HandleReactionAsync(c, r, false, _db);
        }

        private async Task OnReadyAsync()
        {
            // スラッシュコマンドをDiscordに登録
            await _commands.RegisterCommandsGloballyAsync();
            Console.WriteLine("[Ready] Slash commands registered.");
        }

        private async Task HandleInteraction(SocketInteraction interaction)
        {
            var context = new SocketInteractionContext(_client, interaction);
            await _commands.ExecuteCommandAsync(context, _services);
        }
    }
}

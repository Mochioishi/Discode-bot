using Discord.Interactions;
using Discord.WebSocket;
using System.Reflection;

namespace Discord.Handlers;

public class InteractionHandler
{
    private readonly DiscordSocketClient _client;
    private readonly InteractionService _commands;
    private readonly IServiceProvider _services;

    public InteractionHandler(
        DiscordSocketClient client,
        InteractionService commands,
        IServiceProvider services)
    {
        _client = client;
        _commands = commands;
        _services = services;
    }

    public async Task InitializeAsync()
    {
        // モジュール登録
        await _commands.AddModulesAsync(Assembly.GetExecutingAssembly(), _services);

        // イベント登録
        _client.Ready += OnReady;
        _client.InteractionCreated += HandleInteraction;
    }

    private async Task OnReady()
    {
        // ギルドコマンド（即時反映）
        foreach (var guild in _client.Guilds)
        {
            await _commands.RegisterCommandsToGuildAsync(guild.Id);
        }

        Console.WriteLine("SlashCommand registered.");
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
            Console.WriteLine(ex);
        }
    }
}

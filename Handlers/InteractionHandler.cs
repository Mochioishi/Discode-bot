using System.Reflection;
using Discord.Interactions;
using Discord.WebSocket;

namespace DiscordTimeSignal.Handlers;

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
        await _commands.AddModulesAsync(Assembly.GetExecutingAssembly(), _services);

        _client.Ready += OnReady;
        _client.InteractionCreated += HandleInteraction;
    }

    private async Task OnReady()
    {
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
            var ctx = new SocketInteractionContext(_client, interaction);
            await _commands.ExecuteCommandAsync(ctx, _services);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
    }
}

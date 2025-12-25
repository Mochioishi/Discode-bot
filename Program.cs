using Discord;
using Discord.WebSocket;
using Discord.Interactions;
using Discord.Data;
using Discord.Handlers;

var builder = WebApplication.CreateBuilder(args);

// Discord client
builder.Services.AddSingleton(new DiscordSocketClient(new DiscordSocketConfig
{
    GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent
}));

// DataService
builder.Services.AddSingleton<DataService>();

// InteractionService + Handler
builder.Services.AddSingleton<InteractionService>();
builder.Services.AddSingleton<InteractionHandler>();

var app = builder.Build();

var client = app.Services.GetRequiredService<DiscordSocketClient>();
var handler = app.Services.GetRequiredService<InteractionHandler>();

client.Log += msg =>
{
    Console.WriteLine(msg.ToString());
    return Task.CompletedTask;
};

await handler.InitializeAsync();

var token = Environment.GetEnvironmentVariable("DISCORD_TOKEN");
await client.LoginAsync(TokenType.Bot, token);
await client.StartAsync();

await app.RunAsync();

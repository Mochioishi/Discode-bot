using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordTimeSignal.Data;
using DiscordTimeSignal.Handlers;
using DiscordTimeSignal.Workers;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables();

// Discord client
builder.Services.AddSingleton(new DiscordSocketClient(new DiscordSocketConfig
{
    GatewayIntents =
        GatewayIntents.AllUnprivileged |
        GatewayIntents.MessageContent |
        GatewayIntents.GuildMembers |
        GatewayIntents.GuildMessageReactions
}));

builder.Services.AddSingleton<InteractionService>();
builder.Services.AddSingleton<InteractionHandler>();
builder.Services.AddSingleton<DataService>();

// RoleModule を DI に登録（イベント呼び出しに必要）
builder.Services.AddSingleton<RoleModule>();

builder.Services.AddHostedService<TimeSignalWorker>();

var app = builder.Build();

var client = app.Services.GetRequiredService<DiscordSocketClient>();
var handler = app.Services.GetRequiredService<InteractionHandler>();
var roleModule = app.Services.GetRequiredService<RoleModule>();

// ログ
client.Log += msg =>
{
    Console.WriteLine($"{msg.Severity} {msg.Source}\t{msg.Message}");
    return Task.CompletedTask;
};

// InteractionService 初期化
await handler.InitializeAsync();

// ReactionAdded / ReactionRemoved
client.ReactionAdded += async (cache, ch, reaction) =>
{
    await roleModule.OnReactionAdded(cache, ch, reaction);
};

client.ReactionRemoved += async (cache, ch, reaction) =>
{
    await roleModule.OnReactionRemoved(cache, ch, reaction);
};

// Bot 起動
var token = Environment.GetEnvironmentVariable("DISCORD_TOKEN");
await client.LoginAsync(TokenType.Bot, token);
await client.StartAsync();

app.MapGet("/", () => "OK");

await app.RunAsync();

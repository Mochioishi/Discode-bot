using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordTimeSignal.Data;
using DiscordTimeSignal.Handlers;
using DiscordTimeSignal.Workers;
using DiscordTimeSignal.Modules;
using DiscordTimeSignal.Modules.Game.Prsk;
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
        GatewayIntents.Guilds |
        GatewayIntents.GuildMessages |
        GatewayIntents.MessageContent |
        GatewayIntents.GuildMembers |
        GatewayIntents.GuildMessageReactions,

    AlwaysDownloadUsers = true,
    LogGatewayIntentWarnings = false
}));

builder.Services.AddSingleton<InteractionService>();
builder.Services.AddSingleton<InteractionHandler>();
builder.Services.AddSingleton<DataService>();

// Modules（v2 用）
builder.Services.AddTransient<RoleModuleV2>();
builder.Services.AddTransient<PrskRoomIdModule>();

// Worker
builder.Services.AddHostedService<TimeSignalWorker>();

var app = builder.Build();

// DB 初期化
var dataService = app.Services.GetRequiredService<DataService>();
await dataService.EnsureTablesAsync();

var client = app.Services.GetRequiredService<DiscordSocketClient>();
var handler = app.Services.GetRequiredService<InteractionHandler>();

// Modules（DI から取得）
var roleModule = app.Services.GetRequiredService<RoleModuleV2>();
var prskModule = app.Services.GetRequiredService<PrskRoomIdModule>();

// ログ
client.Log += msg =>
{
    Console.WriteLine($"{msg.Severity} {msg.Source}\t{msg.Message}");
    return Task.CompletedTask;
};

// InteractionService 初期化
await handler.InitializeAsync();

// ReactionAdded / ReactionRemoved は v2 では不要
// client.ReactionAdded += roleModule.OnReactionAdded;
// client.ReactionRemoved += roleModule.OnReactionRemoved;

// roomid の MessageReceived を登録
client.MessageReceived += prskModule.OnMessageReceived;

// rolegive v2 のメッセージ受信（リンク・絵文字入力）
client.MessageReceived += roleModule.OnMessageReceived;

// Bot 起動
var token = Environment.GetEnvironmentVariable("DISCORD_TOKEN");
await client.LoginAsync(TokenType.Bot, token);
await client.StartAsync();

app.MapGet("/", () => "OK");

await app.RunAsync();

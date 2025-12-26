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
    GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent | 
                     GatewayIntents.GuildMembers | GatewayIntents.GuildMessageReactions
}));

builder.Services.AddSingleton<InteractionService>();
builder.Services.AddSingleton<InteractionHandler>();
builder.Services.AddSingleton<DataService>();
builder.Services.AddHostedService<TimeSignalWorker>();

var app = builder.Build();

var client = app.Services.GetRequiredService<DiscordSocketClient>();
var handler = app.Services.GetRequiredService<InteractionHandler>();

client.Log += msg =>
{
    Console.WriteLine($"{msg.Severity} {msg.Source}\t{msg.Message}");
    return Task.CompletedTask;
};

await handler.InitializeAsync();

var token = Environment.GetEnvironmentVariable("DISCORD_TOKEN");
await client.LoginAsync(TokenType.Bot, token);
await client.StartAsync();

app.MapGet("/", () => "OK");

await app.RunAsync();

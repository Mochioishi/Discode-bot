using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading.Tasks;
using DiscordBot.Infrastructure;
using DiscordBot.Workers;

namespace DiscordBot
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            using IHost host = Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    var config = new DiscordSocketConfig {
                        GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent | GatewayIntents.GuildMembers | GatewayIntents.GuildMessageReactions,
                        AlwaysDownloadUsers = true
                    };
                    services.AddSingleton(new DiscordSocketClient(config));
                    services.AddSingleton(x => new InteractionService(x.GetRequiredService<DiscordSocketClient>()));
                    services.AddSingleton<InteractionHandler>();
                    services.AddHostedService<TimeSignalWorker>();
                    services.AddHostedService<Worker>();
                })
                .ConfigureHostOptions(options => {
                    options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
                })
                .Build();

            try {
                DbInitializer.Initialize();
                Console.WriteLine("Database initialized.");
            } catch (Exception ex) {
                Console.WriteLine($"DB Initialization Error: {ex.Message}");
            }

            var client = host.Services.GetRequiredService<DiscordSocketClient>();
            var handler = host.Services.GetRequiredService<InteractionHandler>();
            await handler.InitializeAsync();

            string? token = Environment.GetEnvironmentVariable("DISCORD_TOKEN");
            if (!string.IsNullOrWhiteSpace(token)) {
                await client.LoginAsync(TokenType.Bot, token);
                await client.StartAsync();
                await host.RunAsync();
            }
        }
    }
}

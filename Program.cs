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
                        GatewayIntents = GatewayIntents.AllUnprivileged 
                                       | GatewayIntents.MessageContent 
                                       | GatewayIntents.GuildMembers 
                                       | GatewayIntents.GuildMessages 
                                       | GatewayIntents.GuildMessageReactions,
                        AlwaysDownloadUsers = true,
                        MessageCacheSize = 100 
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

            client.Ready += async () =>
            {
                var interactionService = host.Services.GetRequiredService<InteractionService>();

                // --- 【修正箇所】即時反映のための設定 ---
                
                // 1. ここにあなたのテストサーバーのIDを入れてください
                // サーバー名を右クリックして「IDをコピー」で取得できます
                ulong testGuildId = 123456789012345678; // ← ここを書き換える

                if (testGuildId != 0)
                {
                    // 古いギルドコマンドを一度クリアして再登録（一番確実な方法）
                    await interactionService.RegisterCommandsToGuildAsync(testGuildId);
                    Console.WriteLine($"Commands registered to guild: {testGuildId}");
                }

                // グローバル登録は反映が遅いため、開発中はコメントアウト推奨
                // await interactionService.RegisterCommandsGloballyAsync();
                
                // --------------------------------------
            };

            string? token = Environment.GetEnvironmentVariable("DISCORD_TOKEN");
            if (!string.IsNullOrWhiteSpace(token)) {
                await client.LoginAsync(TokenType.Bot, token);
                await client.StartAsync();
                await host.RunAsync();
            }
            else {
                Console.WriteLine("DISCORD_TOKEN is missing.");
            }
        }
    }
}

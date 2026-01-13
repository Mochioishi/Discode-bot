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

            // 1. データベースの初期化
            try {
                DbInitializer.Initialize();
                Console.WriteLine("Database initialized.");
            } catch (Exception ex) {
                Console.WriteLine($"DB Initialization Error: {ex.Message}");
            }

            var client = host.Services.GetRequiredService<DiscordSocketClient>();
            var handler = host.Services.GetRequiredService<InteractionHandler>();
            
            // 2. InteractionHandler（モジュール）の読み込み
            await handler.InitializeAsync();

            // 3. コマンド登録イベントの定義
            client.Ready += async () =>
            {
                var interactionService = host.Services.GetRequiredService<InteractionService>();

                // 環境変数 "SERVER_ID" から取得
                string? guildIdStr = Environment.GetEnvironmentVariable("SERVER_ID");
                
                if (ulong.TryParse(guildIdStr, out ulong testGuildId))
                {
                    // ギルド（サーバー）に対してコマンドを即時登録
                    // これにより、古いスラッシュコマンドが新しいものに上書きされます
                    await interactionService.RegisterCommandsToGuildAsync(testGuildId);
                    Console.WriteLine($"Commands synchronization completed for Guild: {testGuildId}");
                }
                else
                {
                    // SERVER_IDがない場合はグローバル登録
                    await interactionService.RegisterCommandsGloballyAsync();
                    Console.WriteLine("Commands registered globally (may take up to 1 hour).");
                }
            };

            // 4. ボットの起動
            string? token = Environment.GetEnvironmentVariable("DISCORD_TOKEN");
            if (!string.IsNullOrWhiteSpace(token)) {
                await client.LoginAsync(TokenType.Bot, token);
                await client.StartAsync();
                
                // ホストの開始（プログラムの維持）
                await host.RunAsync();
            }
            else {
                Console.WriteLine("CRITICAL ERROR: DISCORD_TOKEN is missing.");
            }
        }
    }
}

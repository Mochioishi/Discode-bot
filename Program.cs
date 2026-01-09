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
                        // 【重要】Intentsに GuildMessages を追加（これがないとPrskの監視ができません）
                        GatewayIntents = GatewayIntents.AllUnprivileged 
                                       | GatewayIntents.MessageContent 
                                       | GatewayIntents.GuildMembers 
                                       | GatewayIntents.GuildMessages 
                                       | GatewayIntents.GuildMessageReactions,
                        AlwaysDownloadUsers = true,
                        // キャッシュ設定を追加するとリアクション処理が安定します
                        MessageCacheSize = 100 
                    };

                    services.AddSingleton(new DiscordSocketClient(config));
                    services.AddSingleton(x => new InteractionService(x.GetRequiredService<DiscordSocketClient>()));
                    
                    // ハンドラーの登録
                    services.AddSingleton<InteractionHandler>();
                    
                    // バックグラウンドワーカーの登録
                    services.AddHostedService<TimeSignalWorker>();
                    services.AddHostedService<Worker>();
                })
                .ConfigureHostOptions(options => {
                    options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
                })
                .Build();

            // データベースの初期化
            try {
                DbInitializer.Initialize();
                Console.WriteLine("Database initialized.");
            } catch (Exception ex) {
                Console.WriteLine($"DB Initialization Error: {ex.Message}");
            }

            var client = host.Services.GetRequiredService<DiscordSocketClient>();
            var handler = host.Services.GetRequiredService<InteractionHandler>();
            
            // InteractionHandlerの初期化（モジュールの読み込み）
            await handler.InitializeAsync();

            // スラッシュコマンドの登録イベント
            client.Ready += async () =>
            {
                // 開発環境や特定のギルドのみに即時反映させたい場合は引数にギルドIDを入れますが、
                // 基本はこのままでグローバル登録されます。
                var interactionService = host.Services.GetRequiredService<InteractionService>();
                await interactionService.RegisterCommandsGloballyAsync();
                Console.WriteLine("Commands registered globally.");
            };

            string? token = Environment.GetEnvironmentVariable("DISCORD_TOKEN");
            if (!string.IsNullOrWhiteSpace(token)) {
                await client.LoginAsync(TokenType.Bot, token);
                await client.StartAsync();
                
                // ホストの開始
                await host.RunAsync();
            }
            else {
                Console.WriteLine("DISCORD_TOKEN is missing.");
            }
        }
    }
}

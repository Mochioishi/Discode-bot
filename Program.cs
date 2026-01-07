using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Services; // WorkerやHandlerの場所
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading.Tasks;

namespace DiscordBot
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            // 1. DB初期化（テーブル作成など）
            try
            {
                DbInitializer.Initialize();
                Console.WriteLine("Database initialized.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DB Initialization Error: {ex.Message}");
            }

            // 2. ホストの構築と実行
            var host = Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    // Discordクライアントの設定
                    var config = new DiscordSocketConfig
                    {
                        GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent | GatewayIntents.GuildMembers,
                        AlwaysDownloadUsers = true
                    };
                    services.AddSingleton(new DiscordSocketClient(config));

                    // スラッシュコマンド用のサービス
                    services.AddSingleton(x => new InteractionService(x.GetRequiredService<DiscordSocketClient>()));

                    // イベントハンドラー (メッセージ監視など)
                    services.AddSingleton<InteractionHandler>();

                    // バックグラウンドWorker (予約投稿・自動削除)
                    services.AddHostedService<TimeSignalWorker>();
                })
                .Build();

            await host.RunAsync();
        }
    }
}

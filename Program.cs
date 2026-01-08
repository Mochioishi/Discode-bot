using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Services;
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
            // 1. DB初期化
            try
            {
                DbInitializer.Initialize();
                Console.WriteLine("Database initialized.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DB Initialization Error: {ex.Message}");
            }

            // 2. ホストの構築
            using IHost host = Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    var config = new DiscordSocketConfig
                    {
                        // メッセージ内容の読み取り、サーバーメンバー、リアクションに必要なインテント
                        GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent | GatewayIntents.GuildMembers | GatewayIntents.GuildMessageReactions,
                        AlwaysDownloadUsers = true
                    };

                    services.AddSingleton(new DiscordSocketClient(config));
                    services.AddSingleton(x => new InteractionService(x.GetRequiredService<DiscordSocketClient>()));
                    
                    // InteractionHandlerをSingletonで登録
                    services.AddSingleton<InteractionHandler>();
                    
                    // TimeSignalWorkerをバックグラウンドサービスとして登録
                    services.AddHostedService<TimeSignalWorker>();
                })
                .Build();

            // 3. 実行前のログイン処理 (ここが重要です)
            var client = host.Services.GetRequiredService<DiscordSocketClient>();
            var handler = host.Services.GetRequiredService<InteractionHandler>(); // ハンドラーをインスタンス化してイベント登録を有効化

            string token = Environment.GetEnvironmentVariable("DISCORD_TOKEN");

            if (string.IsNullOrWhiteSpace(token))
            {
                Console.WriteLine("Error: DISCORD_TOKEN is not set in environment variables.");
                return;
            }

            await client.LoginAsync(TokenType.Bot, token);
            await client.StartAsync();

            // 4. ホストの開始
            await host.RunAsync();
        }
    }
}

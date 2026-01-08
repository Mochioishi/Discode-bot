using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading.Tasks;

// 提案したフォルダ構成に合わせた namespace の参照を追加
using Discord_bot.Infrastructure;
using Discord_bot.Workers;

namespace DiscordBot
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            // 1. ホストの構築
            using IHost host = Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    var config = new DiscordSocketConfig
                    {
                        GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent | GatewayIntents.GuildMembers | GatewayIntents.GuildMessageReactions,
                        AlwaysDownloadUsers = true
                    };

                    services.AddSingleton(new DiscordSocketClient(config));
                    services.AddSingleton(x => new InteractionService(x.GetRequiredService<DiscordSocketClient>()));
                    services.AddSingleton<InteractionHandler>();
                    
                    // バックグラウンドサービス（Workers）の登録
                    services.AddHostedService<TimeSignalWorker>();
                    services.AddHostedService<Worker>(); // 自動削除Workerも登録
                })
                .ConfigureHostOptions(options => {
                    options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
                })
                .Build();

            // 2. DB初期化（Infrastructure フォルダ内の DbInitializer を使用）
            try
            {
                DbInitializer.Initialize();
                Console.WriteLine("Database initialized successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DB Initialization Error: {ex.Message}");
            }

            // 3. ログイン処理
            var client = host.Services.GetRequiredService<DiscordSocketClient>();
            var handler = host.Services.GetRequiredService<InteractionHandler>();

            // コマンドハンドラーの初期化（スラッシュコマンドの登録など）
            await handler.InitializeAsync();

            string? token = Environment.GetEnvironmentVariable("DISCORD_TOKEN");
            if (string.IsNullOrWhiteSpace(token))
            {
                Console.WriteLine("Error: DISCORD_TOKEN is not set.");
                return;
            }

            await client.LoginAsync(TokenType.Bot, token);
            await client.StartAsync();

            // 4. 実行
            await host.RunAsync();
        }
    }
}

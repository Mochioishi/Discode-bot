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
                    services.AddHostedService<TimeSignalWorker>();
                })
                // 【重要】これを入れることで、DBエラー(Workerの失敗)が起きてもBot自体は終了しなくなります
                .ConfigureHostOptions(options => {
                    options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
                })
                .Build();

            // 2. DB初期化（失敗しても次に進むようにtry-catchを維持）
            try
            {
                DbInitializer.Initialize();
                Console.WriteLine("Database initialized.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DB Initialization Error (Skipping for Startup): {ex.Message}");
            }

            // 3. ログイン処理
            var client = host.Services.GetRequiredService<DiscordSocketClient>();
            var handler = host.Services.GetRequiredService<InteractionHandler>();

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

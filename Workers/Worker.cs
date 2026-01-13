using Discord;
using Discord.WebSocket;
using Discord_bot.Infrastructure;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;

namespace Discord_bot.Workers
{
    public class Worker : BackgroundService
    {
        private readonly DiscordSocketClient _client;
        private readonly InteractionHandler _handler;
        private readonly IConfiguration _config;

        public Worker(DiscordSocketClient client, InteractionHandler handler, IConfiguration config)
        {
            _client = client;
            _handler = handler;
            _config = config;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await _handler.InitializeAsync();

            // 環境変数名 DISCORD_TOKEN に合わせる
            string token = _config["DISCORD_TOKEN"] ?? "";
            
            if (string.IsNullOrEmpty(token))
            {
                Console.WriteLine("[Critical] DISCORD_TOKEN is missing in environment variables!");
                return;
            }

            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();

            await Task.Delay(-1, stoppingToken);
        }
    }
}

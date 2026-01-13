using Discord;
using Discord.WebSocket;
using Discord_bot.Infrastructure; // DiscordBot ではなく Discord_bot に修正
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Dapper;

namespace Discord_bot.Workers
{
    public class Worker : BackgroundService
    {
        private readonly DiscordSocketClient _client;
        private readonly InteractionHandler _handler;
        private readonly DbConfig _db;
        private readonly IConfiguration _config;

        public Worker(DiscordSocketClient client, InteractionHandler handler, DbConfig db, IConfiguration config)
        {
            _client = client;
            _handler = handler;
            _db = db;
            _config = config;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // --- 1. Botの起動処理 ---
            await _handler.InitializeAsync();
            
            string token = _config["DiscordToken"] ?? "";
            if (string.IsNullOrEmpty(token))
            {
                Console.WriteLine("[Critical] Discord Token is missing!");
                return;
            }

            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();

            // --- 2. データベースの監視ループ (削除予約のチェック) ---
            // DB初期化が終わるのを少し待機
            await Task.Delay(15000, stoppingToken);
            Console.WriteLine("[Worker] Database monitoring started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var connection = _db.GetConnection();
                    var now = DateTime.UtcNow;

                    // 期限が過ぎた削除予約を取得
                    const string selectSql = "SELECT MessageId, ChannelId FROM ScheduledDeletions WHERE DeleteAt <= @Now";
                    var deletions = await connection.QueryAsync<(long MessageId, long ChannelId)>(selectSql, new { Now = now });

                    foreach (var item in deletions)
                    {
                        var messageId = (ulong)item.MessageId;
                        var channelId = (ulong)item.ChannelId;

                        var channel = await _client.GetChannelAsync(channelId) as ITextChannel;
                        if (channel != null)
                        {
                            try 
                            {
                                var message = await channel.GetMessageAsync(messageId);
                                if (message != null) await message.DeleteAsync();
                            } 
                            catch (Exception ex) 
                            {
                                Console.WriteLine($"[Worker] Message delete failed (ID: {messageId}): {ex.Message}");
                            }
                        }

                        // 処理が終わったレコードを消去
                        await connection.ExecuteAsync("DELETE FROM ScheduledDeletions WHERE MessageId = @MsgId", new { MsgId = item.MessageId });
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Worker DB Error]: {ex.Message}");
                }

                // 1分ごとにチェック
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            await _client.StopAsync();
            await base.StopAsync(cancellationToken);
        }
    }
}

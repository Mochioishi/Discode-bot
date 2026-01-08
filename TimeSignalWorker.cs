using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordBot.Services
{
    public class TimeSignalWorker : BackgroundService
    {
        private readonly DiscordSocketClient _client;
        private readonly string _connectionString;
        private readonly string _targetChannelId;
        private readonly TimeZoneInfo _tzi = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tokyo");

        public TimeSignalWorker(DiscordSocketClient client)
        {
            _client = client;
            _connectionString = DbConfig.GetConnectionString();
            _targetChannelId = Environment.GetEnvironmentVariable("TARGET_CHANNEL_ID") ?? "";
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Console.WriteLine($"Worker active with TimeZone: Asia/Tokyo");

            while (!stoppingToken.IsCancellationRequested)
            {
                var now = TimeZoneInfo.ConvertTime(DateTimeOffset.Now, _tzi);
                var timeStr = now.ToString("HH:mm");

                // 平日（月〜金）のみ実行
                if (now.DayOfWeek != DayOfWeek.Saturday && now.DayOfWeek != DayOfWeek.Sunday)
                {
                    // 指定の時間にメッセージを送信
                    if (timeStr == "08:25" || timeStr == "12:55" || timeStr == "17:20")
                    {
                        await SendAlarmAsync();
                    }
                }

                // 毎分、期限切れメッセージをDBから確認して削除
                await ProcessScheduledMessages(timeStr);

                // 1分待機
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        private async Task SendAlarmAsync()
        {
            if (ulong.TryParse(_targetChannelId, out var channelId))
            {
                var channel = _client.GetChannel(channelId) as IMessageChannel;
                if (channel != null)
                {
                    await channel.SendMessageAsync("🔆アラーム！");
                }
            }
        }

        private async Task ProcessScheduledMessages(string time)
        {
            // ここがログの92行目付近です。
            // try-catchで囲むことで、パスワードエラーが起きてもBotが終了するのを防ぎます。
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();

                var messagesToDelete = new List<(ulong ChannelId, ulong MessageId)>();

                using (var cmd = new NpgsqlCommand("SELECT ChannelId, MessageId FROM ScheduledDeletions WHERE DeleteAt = @time", conn))
                {
                    cmd.Parameters.AddWithValue("time", time);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            messagesToDelete.Add(((ulong)reader.GetInt64(0), (ulong)reader.GetInt64(1)));
                        }
                    }
                }

                foreach (var (channelId, messageId) in messagesToDelete)
                {
                    try
                    {
                        var channel = _client.GetChannel(channelId) as IMessageChannel;
                        if (channel != null)
                        {
                            await channel.DeleteMessageAsync(messageId);
                        }

                        using (var delCmd = new NpgsqlCommand("DELETE FROM ScheduledDeletions WHERE MessageId = @mid", conn))
                        {
                            delCmd.Parameters.AddWithValue("mid", (long)messageId);
                            await delCmd.ExecuteNonQueryAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Worker] Failed to delete message {messageId}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                // DBエラーが起きても、ログを出力するだけで上位には例外を投げない
                Console.WriteLine($"[Worker DB Connection Error]: {ex.Message}");
                // 認証失敗(28P01)などの場合は、ここで処理を中断して次のループへ回す
            }
        }
    }
}

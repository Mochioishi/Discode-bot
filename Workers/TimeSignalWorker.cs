using Discord;
using Discord.WebSocket;
using DiscordBot.Infrastructure;
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
        private string _connectionString;
        private readonly string _targetChannelId;
        private readonly TimeZoneInfo _tzi = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tokyo");

        public TimeSignalWorker(DiscordSocketClient client)
        {
            _client = client;
            _targetChannelId = Environment.GetEnvironmentVariable("TARGET_CHANNEL_ID") ?? "";
            
            // 起動時にDB接続文字列の読み込みに失敗しても、プログラムを落とさない
            try
            {
                _connectionString = DbConfig.GetConnectionString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Critical] Failed to load ConnectionString in Worker: {ex.Message}");
                _connectionString = ""; // 空文字で初期化してクラッシュを防ぐ
            }
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

                // 定期的なDB処理もエラーで止まらないように実行
                await ProcessScheduledMessages(timeStr);

                // 1分待機
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        private async Task SendAlarmAsync()
        {
            if (ulong.TryParse(_targetChannelId, out var channelId))
            {
                var channel = await _client.GetChannelAsync(channelId) as IMessageChannel;
                if (channel != null)
                {
                    await channel.SendMessageAsync("🔆アラーム！");
                }
            }
        }

        private async Task ProcessScheduledMessages(string time)
        {
            // 接続文字列が空、または形式が不正な場合は処理をスキップ
            if (string.IsNullOrEmpty(_connectionString)) return;

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
                        var channel = await _client.GetChannelAsync(channelId) as IMessageChannel;
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
                // DBエラーが起きてもログを出して続行
                Console.WriteLine($"[Worker DB Error]: {ex.Message}");
                
                // もし接続文字列自体のエラー(ArgumentException)が起きていた場合、
                // 再読み込みを試みることで、Railway側で変数を直した際に反映される可能性があります
                if (ex is ArgumentException) {
                     try { _connectionString = DbConfig.GetConnectionString(); } catch { }
                }
            }
        }
    }
}

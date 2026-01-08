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
            
            try
            {
                _connectionString = DbConfig.GetConnectionString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Critical] Failed to load ConnectionString in Worker: {ex.Message}");
                _connectionString = "";
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Console.WriteLine($"Worker active with TimeZone: Asia/Tokyo");

            while (!stoppingToken.IsCancellationRequested)
            {
                var now = TimeZoneInfo.ConvertTime(DateTimeOffset.Now, _tzi);
                var timeStr = now.ToString("HH:mm");

                if (now.DayOfWeek != DayOfWeek.Saturday && now.DayOfWeek != DayOfWeek.Sunday)
                {
                    if (timeStr == "08:25" || timeStr == "12:55" || timeStr == "17:20")
                    {
                        await SendAlarmAsync();
                    }
                }

                await ProcessScheduledMessages(timeStr);
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
            if (string.IsNullOrEmpty(_connectionString)) return;

            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();

                var messagesToDelete = new List<(ulong ChannelId, ulong MessageId)>();

                // 【修正箇所】テーブル名とカラム名を \" で囲みました
                var selectSql = "SELECT \"ChannelId\", \"MessageId\" FROM \"ScheduledDeletions\" WHERE \"DeleteAt\"::text LIKE @time || '%'";

                using (var cmd = new NpgsqlCommand(selectSql, conn))
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

                        // 【修正箇所】ここも \" で囲みました
                        var deleteSql = "DELETE FROM \"ScheduledDeletions\" WHERE \"MessageId\" = @mid";
                        using (var delCmd = new NpgsqlCommand(deleteSql, conn))
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
                Console.WriteLine($"[TimeSignalWorker DB Error]: {ex.Message}");
                if (ex is ArgumentException) {
                     try { _connectionString = DbConfig.GetConnectionString(); } catch { }
                }
            }
        }
    }
}

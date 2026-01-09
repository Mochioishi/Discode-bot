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

                // 1. 平日アラーム (既存機能)
                if (now.DayOfWeek != DayOfWeek.Saturday && now.DayOfWeek != DayOfWeek.Sunday)
                {
                    if (timeStr == "08:25" || timeStr == "12:55" || timeStr == "17:20")
                    {
                        await SendAlarmAsync();
                    }
                }

                // 2. 以前のBotText予約投稿のチェック (追加機能)
                await ProcessBotTextSchedules(timeStr);

                // 3. 自動削除メッセージのチェック (既存機能)
                await ProcessScheduledDeletions(timeStr);

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

        // --- 以前の BotText 予約送信ロジック ---
        private async Task ProcessBotTextSchedules(string time)
        {
            if (string.IsNullOrEmpty(_connectionString)) return;
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();

                // 修正後のカラム名(Text, Title, ShowTime, ChannelId)に合わせたSQL
                var sql = "SELECT \"Text\", \"Title\", \"ShowTime\", \"ChannelId\" FROM \"BotTextSchedules\" WHERE \"ScheduledTime\" = @tm";
                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("tm", time);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var text = reader.GetString(0);
                    var title = reader.GetString(1);
                    var showTime = reader.GetBoolean(2);
                    if (ulong.TryParse(reader.GetString(3), out var cid))
                    {
                        var channel = await _client.GetChannelAsync(cid) as IMessageChannel;
                        if (channel != null)
                        {
                            var eb = new EmbedBuilder()
                                .WithTitle(title)
                                .WithDescription(text)
                                .WithColor(new Color(0x3498db));
                            if (showTime) eb.WithCurrentTimestamp();

                            await channel.SendMessageAsync(embed: eb.Build());
                        }
                    }
                }
            }
            catch (Exception ex) { Console.WriteLine($"[Worker BotText Error]: {ex.Message}"); }
        }

        // --- 自動削除 (ScheduledDeletions) ロジック ---
        private async Task ProcessScheduledDeletions(string time)
        {
            if (string.IsNullOrEmpty(_connectionString)) return;
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();

                var messagesToDelete = new List<(ulong ChannelId, ulong MessageId)>();
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
                        if (channel != null) await channel.DeleteMessageAsync(messageId);

                        using var delCmd = new NpgsqlCommand("DELETE FROM \"ScheduledDeletions\" WHERE \"MessageId\" = @mid", conn);
                        delCmd.Parameters.AddWithValue("mid", (long)messageId);
                        await delCmd.ExecuteNonQueryAsync();
                    }
                    catch (Exception ex) { Console.WriteLine($"[Worker Delete Error]: {ex.Message}"); }
                }
            }
            catch (Exception ex) { Console.WriteLine($"[Worker DB Error]: {ex.Message}"); }
        }
    }
}

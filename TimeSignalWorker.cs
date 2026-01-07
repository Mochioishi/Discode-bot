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
        private readonly TimeZoneInfo _jst;

        public TimeSignalWorker(DiscordSocketClient client)
        {
            _client = client;
            _connectionString = GetConnectionString();
            
            var tzId = Environment.GetEnvironmentVariable("TIMEZONE") ?? "Asia/Tokyo";
            _jst = TimeZoneInfo.FindSystemTimeZoneById(tzId);
        }

        private string GetConnectionString()
        {
            var url = Environment.GetEnvironmentVariable("DATABASE_URL");
            if (string.IsNullOrEmpty(url)) return "Host=localhost;Username=postgres;Password=password;Database=discord_bot";
            var uri = new Uri(url);
            var userInfo = uri.UserInfo.Split(':');
            return new NpgsqlConnectionStringBuilder
            {
                Host = uri.Host, Port = uri.Port, Username = userInfo[0], Password = userInfo[1],
                Database = uri.LocalPath.TrimStart('/'), SslMode = SslMode.Require, TrustServerCertificate = true
            }.ToString();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (_client.LoginState != LoginState.LoggedIn) await Task.Delay(5000, stoppingToken);

            Console.WriteLine("Worker active with TimeZone: Asia/Tokyo");

            while (!stoppingToken.IsCancellationRequested)
            {
                var nowJst = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _jst);
                string currentTime = nowJst.ToString("HHmm");
                DayOfWeek today = nowJst.DayOfWeek;

                // --- 固定アラーム（平日のみ、削除・一覧化不可） ---
                if (today != DayOfWeek.Saturday && today != DayOfWeek.Sunday)
                {
                    if (currentTime == "0825" || currentTime == "1255" || currentTime == "1720")
                    {
                        await SendFixedAlarm();
                    }
                }

                // 1. 予約投稿のチェック (毎分)
                await ProcessScheduledMessages(currentTime);

                // 2. 自動削除のチェック (毎日 04:00)
                if (currentTime == "0400")
                {
                    await ProcessAutoPurge();
                }

                // 次の00秒まで待機
                await Task.Delay(60000 - (nowJst.Second * 1000), stoppingToken);
            }
        }

        // --- 固定アラーム送信ロジック ---
        private async Task SendFixedAlarm()
        {
            var targetIdEnv = Environment.GetEnvironmentVariable("TARGET_CHANNEL_ID");
            if (ulong.TryParse(targetIdEnv, out var channelId))
            {
                if (await _client.GetChannelAsync(channelId) is ITextChannel channel)
                {
                    await channel.SendMessageAsync("🔆アラーム！");
                }
            }
        }

        // --- 予約投稿ロジック ---
        private async Task ProcessScheduledMessages(string time)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            var messages = new List<ScheduledInfo>();

            using (var cmd = new NpgsqlCommand("SELECT id, channel_id, content, is_embed, embed_title FROM scheduled_messages WHERE scheduled_time = @time", conn))
            {
                cmd.Parameters.AddWithValue("time", time);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    messages.Add(new ScheduledInfo {
                        Id = reader.GetInt32(0), ChannelId = ulong.Parse(reader.GetString(1)),
                        Content = reader.GetString(2), IsEmbed = reader.GetBoolean(3),
                        Title = reader.IsDBNull(4) ? null : reader.GetString(4)
                    });
                }
            }

            foreach (var msg in messages)
            {
                if (await _client.GetChannelAsync(msg.ChannelId) is IMessageChannel channel)
                {
                    if (msg.IsEmbed)
                    {
                        var eb = new EmbedBuilder().WithTitle(msg.Title).WithDescription(msg.Content).WithColor(Color.Blue).Build();
                        await channel.SendMessageAsync(embed: eb);
                    }
                    else await channel.SendMessageAsync(msg.Content);

                    using var del = new NpgsqlCommand("DELETE FROM scheduled_messages WHERE id = @id", conn);
                    del.Parameters.AddWithValue("id", msg.Id);
                    await del.ExecuteNonQueryAsync();
                }
            }
        }

        // --- 自動削除ロジック (deleteago) ---
        private async Task ProcessAutoPurge()
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            
            using var cmd = new NpgsqlCommand("SELECT channel_id, days_ago, protection_type FROM auto_purge_settings", conn);
            using var reader = await cmd.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                var channelId = ulong.Parse(reader.GetString(0));
                var days = reader.GetInt32(1);
                var protection = reader.GetString(2); 

                if (await _client.GetChannelAsync(channelId) is ITextChannel channel)
                {
                    var cutoffDate = DateTimeOffset.UtcNow.AddDays(-days);
                    var messages = channel.GetMessagesAsync(100).Flatten();

                    await foreach (var message in messages)
                    {
                        if (message.CreatedAt < cutoffDate)
                        {
                            bool hasImage = message.Attachments.Count > 0;
                            bool hasReaction = message.Reactions.Count > 0;

                            bool shouldProtect = protection switch {
                                "Image" => hasImage,
                                "Reaction" => hasReaction,
                                "Both" => hasImage || hasReaction,
                                _ => false
                            };

                            if (!shouldProtect) await message.DeleteAsync();
                        }
                    }
                }
            }
        }

        private class ScheduledInfo { public int Id; public ulong ChannelId; public string Content; public bool IsEmbed; public string? Title; }
    }
}

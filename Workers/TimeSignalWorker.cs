using Discord;
using Discord.WebSocket;
using Discord_bot.Infrastructure;
using Dapper;
using Microsoft.Extensions.Hosting;

namespace Discord_bot.Workers
{
    public class TimeSignalWorker : BackgroundService
    {
        private readonly DiscordSocketClient _client;
        private readonly DbConfig _db;
        private readonly string _targetChannelId;
        private readonly TimeZoneInfo _tzi = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tokyo");

        public TimeSignalWorker(DiscordSocketClient client, DbConfig db)
        {
            _client = client;
            _db = db;
            // 環境変数または設定からアラーム用チャンネルIDを取得
            _targetChannelId = Environment.GetEnvironmentVariable("TARGET_CHANNEL_ID") ?? "";
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Console.WriteLine($"[Worker] Active with TimeZone: Asia/Tokyo");

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

                // 2. 予約投稿 (bottext) のチェック
                await ProcessBotTextSchedules(timeStr);

                // 3. 午前4時の自動削除 (deleteago) の実行
                if (timeStr == "04:00")
                {
                    await ExecuteAutoDeleteAgo();
                }

                // 次の00秒まで待機（毎分実行の精度を上げるため）
                await Task.Delay(TimeSpan.FromSeconds(60 - DateTime.Now.Second), stoppingToken);
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

        // --- 予約投稿 (bottext) 実行ロジック ---
        private async Task ProcessBotTextSchedules(string time)
        {
            using var conn = _db.GetConnection();
            const string sql = "SELECT * FROM BotTextSchedules WHERE ScheduledTime = @tm";
            var schedules = await conn.QueryAsync(sql, new { tm = time });

            foreach (var s in schedules)
            {
                try
                {
                    var channel = await _client.GetChannelAsync((ulong)s.ChannelId) as IMessageChannel;
                    if (channel != null)
                    {
                        if (s.IsEmbed)
                        {
                            var eb = new EmbedBuilder()
                                .WithTitle(s.Title)
                                .WithDescription(s.Text)
                                .WithColor(Color.Blue)
                                .WithCurrentTimestamp() // 設計図のshow_timeに相当
                                .Build();
                            await channel.SendMessageAsync(embed: eb);
                        }
                        else
                        {
                            await channel.SendMessageAsync(s.Text);
                        }
                    }
                    // 送信完了したら削除
                    await conn.ExecuteAsync("DELETE FROM BotTextSchedules WHERE Id = @id", new { id = s.Id });
                }
                catch (Exception ex) { Console.WriteLine($"[Worker BotText Error]: {ex.Message}"); }
            }
        }

        // --- 午前4時の自動削除 (deleteago) ロジック ---
        private async Task ExecuteAutoDeleteAgo()
        {
            using var conn = _db.GetConnection();
            var configs = await conn.QueryAsync("SELECT * FROM DeleteConfigs");

            foreach (var config in configs)
            {
                try
                {
                    var channel = await _client.GetChannelAsync((ulong)config.ChannelId) as ITextChannel;
                    if (channel == null) continue;

                    // 指定された日数より前のメッセージを取得
                    var beforeDate = DateTimeOffset.Now.AddDays(-(int)config.Days);
                    var messages = await channel.GetMessagesAsync(100).FlattenAsync(); // 簡易的に直近100件

                    var targets = messages.Where(m => m.Timestamp < beforeDate).ToList();
                    
                    // 保護ルールの適用
                    var toDelete = targets.Where(m => {
                        bool hasImg = m.Attachments.Any(a => a.ContentType?.StartsWith("image/") == true);
                        bool hasReact = m.Reactions.Count > 0;
                        return (int)config.ProtectType switch {
                            1 => !hasImg,
                            2 => !hasReact,
                            3 => !hasImg && !hasReact,
                            _ => true
                        };
                    }).ToList();

                    if (toDelete.Any()) await channel.DeleteMessagesAsync(toDelete);
                }
                catch (Exception ex) { Console.WriteLine($"[Worker DeleteAgo Error]: {ex.Message}"); }
            }
        }
    }
}

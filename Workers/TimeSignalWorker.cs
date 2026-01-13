using Discord;
using Discord.WebSocket;
using Discord_bot.Infrastructure;
using Dapper;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;

namespace Discord_bot.Workers
{
    public class TimeSignalWorker : BackgroundService
    {
        private readonly DiscordSocketClient _client;
        private readonly DbConfig _db;
        private readonly IConfiguration _config;
        private readonly TimeZoneInfo _tzi = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tokyo");

        public TimeSignalWorker(DiscordSocketClient client, DbConfig db, IConfiguration config)
        {
            _client = client;
            _db = db;
            _config = config;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var now = TimeZoneInfo.ConvertTime(DateTimeOffset.Now, _tzi);
                    var timeStr = now.ToString("HH:mm");

                    // 平日アラーム
                    if (now.DayOfWeek != DayOfWeek.Saturday && now.DayOfWeek != DayOfWeek.Sunday)
                    {
                        if (timeStr == "08:25" || timeStr == "12:55" || timeStr == "17:20")
                        {
                            await SendAlarmAsync();
                        }
                    }

                    await ProcessBotTextSchedules(timeStr);

                    if (timeStr == "04:00")
                    {
                        await ExecuteAutoDeleteAgo();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[TimeSignalWorker Error] {ex.Message}");
                }

                await Task.Delay(TimeSpan.FromSeconds(60 - DateTime.Now.Second), stoppingToken);
            }
        }

        private async Task SendAlarmAsync()
        {
            if (ulong.TryParse(_config["TARGET_CHANNEL_ID"], out var channelId))
            {
                var channel = await _client.GetChannelAsync(channelId) as IMessageChannel;
                if (channel != null) await channel.SendMessageAsync("🔆アラーム！");
            }
        }

        private async Task ProcessBotTextSchedules(string time)
        {
            using var conn = _db.GetConnection();
            var schedules = await conn.QueryAsync("SELECT * FROM BotTextSchedules WHERE ScheduledTime = @tm", new { tm = time });
            foreach (var s in schedules)
            {
                var ch = await _client.GetChannelAsync((ulong)s.ChannelId) as IMessageChannel;
                if (ch != null) await ch.SendMessageAsync(s.Text);
                await conn.ExecuteAsync("DELETE FROM BotTextSchedules WHERE Id = @id", new { id = s.Id });
            }
        }

        private async Task ExecuteAutoDeleteAgo()
        {
            using var conn = _db.GetConnection();
            var configs = await conn.QueryAsync("SELECT * FROM DeleteConfigs");
            // ... (削除ロジック)
        }
    }
}

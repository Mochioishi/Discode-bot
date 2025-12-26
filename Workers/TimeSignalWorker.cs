using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;

namespace DiscordTimeSignal.Workers;

public class TimeSignalWorker : BackgroundService
{
    private readonly DiscordSocketClient _client;

    // 固定チャンネルID
    private const ulong TARGET_CHANNEL_ID = 123456789012345678; // ← ここを書き換える

    // 平日のみ
    private static readonly DayOfWeek[] Weekdays =
    {
        DayOfWeek.Monday,
        DayOfWeek.Tuesday,
        DayOfWeek.Wednesday,
        DayOfWeek.Thursday,
        DayOfWeek.Friday
    };

    // アラーム時刻
    private static readonly TimeOnly[] AlarmTimes =
    {
        new TimeOnly(8, 28),
        new TimeOnly(12, 55),
        new TimeOnly(17, 55)
    };

    public TimeSignalWorker(DiscordSocketClient client)
    {
        _client = client;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndSendAlarms();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TimeSignalWorker ERROR] {ex}");
            }

            await Task.Delay(1000, stoppingToken); // 1秒ごとにチェック
        }
    }

    private async Task CheckAndSendAlarms()
    {
        if (_client.LoginState != Discord.LoginState.LoggedIn)
            return;

        var now = DateTime.Now;

        // 平日以外は無視
        if (!Weekdays.Contains(now.DayOfWeek))
            return;

        var nowTime = TimeOnly.FromDateTime(now);

        foreach (var alarm in AlarmTimes)
        {
            // 時刻が一致した瞬間だけ送信（秒まで一致）
            if (nowTime.Hour == alarm.Hour &&
                nowTime.Minute == alarm.Minute &&
                now.Second == 0)
            {
                var channel = _client.GetChannel(TARGET_CHANNEL_ID) as IMessageChannel;
                if (channel != null)
                {
                    await channel.SendMessageAsync("🔆 アラーム！");
                }
            }
        }
    }
}

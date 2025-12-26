using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;

namespace DiscordTimeSignal.Workers;

public class TimeSignalWorker : BackgroundService
{
    private readonly DiscordSocketClient _client;
    private readonly ulong _targetChannelId;

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

        // Railway の環境変数から読み込む
        var env = Environment.GetEnvironmentVariable("ALARM_CHANNEL_ID");

        if (!ulong.TryParse(env, out _targetChannelId))
        {
            Console.WriteLine("[TimeSignalWorker] ERROR: ALARM_CHANNEL_ID が不正です。");
            _targetChannelId = 0;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Bot が完全にログインするまで待つ
        while (_client.LoginState != LoginState.LoggedIn)
            await Task.Delay(1000, stoppingToken);

        Console.WriteLine("[TimeSignalWorker] Started");

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
        if (_targetChannelId == 0)
            return;

        var now = DateTime.Now;

        // 平日以外は無視
        if (!Weekdays.Contains(now.DayOfWeek))
            return;

        var nowTime = TimeOnly.FromDateTime(now);

        foreach (var alarm in AlarmTimes)
        {
            if (nowTime.Hour == alarm.Hour &&
                nowTime.Minute == alarm.Minute &&
                now.Second == 0)
            {
                var channel = _client.GetChannel(_targetChannelId) as IMessageChannel;
                if (channel != null)
                {
                    await channel.SendMessageAsync("🔆 アラーム！");
                    Console.WriteLine($"[TimeSignalWorker] Sent alarm at {nowTime}");
                }
                else
                {
                    Console.WriteLine("[TimeSignalWorker] ERROR: チャンネルが見つかりません");
                }
            }
        }
    }
}

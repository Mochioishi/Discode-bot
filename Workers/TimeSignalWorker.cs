using Discord;
using Discord.WebSocket;
using DiscordTimeSignal.Data;
using Microsoft.Extensions.Hosting;

namespace DiscordTimeSignal.Workers;

public class TimeSignalWorker : BackgroundService
{
    private readonly DiscordSocketClient _client;
    private readonly DataService _data;

    public TimeSignalWorker(DiscordSocketClient client, DataService data)
    {
        _client = client;
        _data = data;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var nowJst = DateTime.UtcNow.AddHours(9);
            var hhmm = nowJst.ToString("HH:mm");

            // bottext 実行
            await RunBotTextAsync(hhmm);

            // deleteago は午前4時だけ
            if (hhmm == "04:00")
            {
                await RunDeleteAgoAsync();
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private async Task RunBotTextAsync(string hhmm)
    {
        // 全ギルド・全チャンネルを走査する設計にしていないので、
        // bottext には "time_hhmm" で絞り込む SQL を DataService に追加してもOK。
        // ここでは簡略的に全件取得パターンは省略し、
        // 必要なら DataService に専用メソッドを追加する形を想定。
    }

    private async Task RunDeleteAgoAsync()
    {
        var entries = await _data.GetAllDeleteAgoAsync();

        foreach (var entry in entries)
        {
            var guild = _client.GetGuild(entry.GuildId);
            if (guild == null) continue;
            var channel = guild.GetTextChannel(entry.ChannelId);
            if (channel == null) continue;

            var cutoff = DateTimeOffset.UtcNow.AddDays(-entry.Days);

            var messages = await channel.GetMessagesAsync(limit: 1000).FlattenAsync();

            foreach (var msg in messages)
            {
                if (msg.Timestamp >= cutoff) continue;

                if (!ShouldDeleteMessage(msg, entry.ProtectMode))
                    continue;

                try
                {
                    await msg.DeleteAsync();
                }
                catch
                {
                    // ログだけ出すならここ
                }
            }
        }
    }

    private bool ShouldDeleteMessage(IMessage msg, string mode)
    {
        var hasImage = msg.Attachments.Any(a => a.ContentType?.StartsWith("image/") == true);
        var hasReaction = msg.Reactions?.Count > 0;

        return mode switch
        {
            "image" => !hasImage,
            "reaction" => !hasReaction,
            "both" => !(hasImage || hasReaction),
            _ => true
        };
    }
}

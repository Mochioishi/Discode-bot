using Discord;
using Discord.WebSocket;
using DiscordTimeSignal.Data;

namespace DiscordTimeSignal.Workers;

public class TimeSignalWorker : BackgroundService
{
    private readonly DiscordSocketClient _client;
    private readonly DataService _db;
    private readonly ILogger<TimeSignalWorker> _logger;

    public TimeSignalWorker(DiscordSocketClient client, DataService db, ILogger<TimeSignalWorker> logger)
    {
        _client = client;
        _db = db;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.Now;
            string currentTime = now.ToString("HH:mm");

            // --- 1. bottext の予約送信チェック ---
            await ProcessScheduledMessages(currentTime);

            // --- 2. deleteago の実行チェック (午前4時) ---
            if (currentTime == "04:00")
            {
                await ProcessAutoCleanup();
            }

            // 1分待機
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private async Task ProcessScheduledMessages(string time)
    {
        var tasks = await _db.GetTasksByTimeAsync(time);
        foreach (var task in tasks)
        {
            var channel = _client.GetChannel(task.ChannelId) as IMessageChannel;
            if (channel == null) continue;

            if (task.IsEmbed)
            {
                var embed = new EmbedBuilder()
                    .WithTitle(task.EmbedTitle)
                    .WithDescription(task.Content)
                    .WithColor(Color.Blue).Build();
                await channel.SendMessageAsync(embed: embed);
            }
            else
            {
                await channel.SendMessageAsync(task.Content);
            }
        }
    }

    private async Task ProcessAutoCleanup()
    {
        var settings = await _db.GetAllCleanupSettingsAsync();
        foreach (var set in settings)
        {
            var channel = _client.GetChannel(set.ChannelId) as ITextChannel;
            if (channel == null) continue;

            // 指定日数前の基準時間を計算
            var threshold = DateTimeOffset.UtcNow.AddDays(-set.DaysBefore);
            var messages = await channel.GetMessagesAsync(limit: 100).FlattenAsync();

            foreach (var msg in messages)
            {
                if (msg.CreatedAt < threshold)
                {
                    // 保護対象の判定ロジック
                    bool hasImage = msg.Attachments.Any(x => x.Width != null);
                    bool hasReaction = msg.Reactions.Count > 0;

                    bool shouldDelete = set.ProtectionType switch
                    {
                        "image" => !hasImage,
                        "reaction" => !hasReaction,
                        "both" => !hasImage && !hasReaction,
                        _ => true // なし（すべて削除）
                    };

                    if (shouldDelete) await msg.DeleteAsync();
                }
            }
        }
    }
}

using Discord;
using Discord.WebSocket;
using Discord.Data;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Discord.Workers; // 名前空間を Discord に統一

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
        // クライアントが準備できるまで待機
        while (_client.ConnectionState != ConnectionState.Connected)
        {
            await Task.Delay(5000, stoppingToken);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.Now;
            string currentTimeStr = now.ToString("HH:mm");

            // --- 1. 予約送信チェック ---
            // 現在時刻を DateTime として渡す（DataServiceの定義に合わせる）
            await ProcessScheduledMessages(now);

            // --- 2. 自動削除チェック (午前4時) ---
            if (currentTimeStr == "04:00")
            {
                await ProcessAutoCleanup();
            }

            // 1分待機
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private async Task ProcessScheduledMessages(DateTime time)
    {
        try 
        {
            // DataService の引数型 (DateTime) に合わせて呼び出し
            var tasks = await _db.GetTasksByTimeAsync(time);
            
            foreach (var task in tasks)
            {
                var channel = _client.GetChannel(task.ChannelId) as IMessageChannel;
                if (channel == null) continue;

                // 現在の DataModels.cs に Embed 関連のフラグがないため、一旦通常のテキスト送信に統一
                // もし Embed が必要な場合は DataModels にプロパティを追加する必要があります
                await channel.SendMessageAsync(task.Content);

                // 送信完了したタスクを削除（これを行わないと毎分送られてしまいます）
                await _db.DeleteMessageTaskAsync(task.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "予約メッセージの処理中にエラーが発生しました。");
        }
    }

    private async Task ProcessAutoCleanup()
    {
        try
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
                        // 画像やリアクションの有無を判定
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "自動クリーンアップ処理中にエラーが発生しました。");
        }
    }
}

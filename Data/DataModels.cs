namespace DiscordTimeSignal.Data;

public class BotMessageTask
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public ulong ChannelId { get; set; }
    public string Content { get; set; } = string.Empty;
    public bool IsEmbed { get; set; }
    public string? EmbedTitle { get; set; }
    public string? ScheduledTime { get; set; } // "HH:mm" 形式 (nullなら即時)
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class GameRoomConfig
{
    public ulong GuildId { get; set; }
    public ulong MonitorChannelId { get; set; } // 数字を監視する場所
    public ulong TargetChannelId { get; set; }  // 名前を変える場所
    public string OriginalNameFormat { get; set; } = "【roomid】"; // 変更後の雛形
}

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

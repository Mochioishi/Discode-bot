namespace DiscordTimeSignal.Data;

public class BotTextEntry
{
    public long Id { get; set; }
    public ulong GuildId { get; set; }
    public ulong ChannelId { get; set; }
    public string Content { get; set; } = "";
    public bool IsEmbed { get; set; }
    public string? EmbedTitle { get; set; }
    public string TimeHhmm { get; set; } = "";
}

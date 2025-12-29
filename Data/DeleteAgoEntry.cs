namespace DiscordTimeSignal.Data;

public class DeleteAgoEntry
{
    public long Id { get; set; }
    public ulong GuildId { get; set; }
    public ulong ChannelId { get; set; }
    public int Days { get; set; }
    public string ProtectMode { get; set; } = "none";
}

namespace DiscordTimeSignal.Data;

public class RoleGiveEntry
{
    public long Id { get; set; }
    public ulong GuildId { get; set; }
    public ulong ChannelId { get; set; }
    public ulong MessageId { get; set; }
    public ulong RoleId { get; set; }
    public string Emoji { get; set; } = "";
}

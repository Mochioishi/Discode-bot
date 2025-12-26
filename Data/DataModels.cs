namespace DiscordTimeSignal.Data;

public record BotTextEntry
{
    public long Id { get; init; }
    public ulong GuildId { get; init; }
    public ulong ChannelId { get; init; }
    public string Content { get; init; } = "";
    public bool IsEmbed { get; init; }
    public string? EmbedTitle { get; init; }
    public string TimeHhmm { get; init; } = "00:00";
}

public record DeleteAgoEntry
{
    public long Id { get; init; }
    public ulong GuildId { get; init; }
    public ulong ChannelId { get; init; }
    public int Days { get; init; }
    public string ProtectMode { get; init; } = "none";
}

public record PrskRoomIdEntry
{
    public long Id { get; init; }
    public ulong GuildId { get; init; }
    public ulong WatchChannelId { get; init; }
    public ulong TargetChannelId { get; init; }
    public string NameFormat { get; init; } = "ex【{roomid}】";
}

public record RoleGiveEntry
{
    public long Id { get; init; }
    public ulong GuildId { get; init; }
    public ulong ChannelId { get; init; }
    public ulong MessageId { get; init; }
    public ulong RoleId { get; init; }
    public string Emoji { get; init; } = "🐾";
}

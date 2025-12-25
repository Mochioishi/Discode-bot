using System;

namespace Discord.Data
{
    public class CleanupSetting {
        public ulong GuildId { get; set; }
        public ulong ChannelId { get; set; }
    }

    public class GameRoomConfig {
        public ulong GuildId { get; set; }
        public ulong MonitorChannelId { get; set; }
        public ulong TargetChannelId { get; set; }
        public string? OriginalNameFormat { get; set; }
    }

    public class RoleGiveConfig {
        public ulong MessageId { get; set; }
        public ulong RoleId { get; set; }
        public string? EmojiName { get; set; }
    }

    public class MessageTask {
        public int Id { get; set; }
        public ulong ChannelId { get; set; }
        public string? Content { get; set; }
        public DateTime ScheduledTime { get; set; }
    }
}

using Dapper;
using Npgsql;
using Microsoft.Extensions.Configuration;

namespace Discord.Data
{
    public class DataService
    {
        private readonly string _connectionString;

        public DataService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") 
                               ?? Environment.GetEnvironmentVariable("DATABASE_URL") 
                               ?? "";
        }

        private NpgsqlConnection GetConn() => new NpgsqlConnection(_connectionString);

        // --- Cleanup ---
        public async Task<IEnumerable<CleanupSetting>> GetAllCleanupSettingsAsync() => await GetConn().QueryAsync<CleanupSetting>("SELECT * FROM CleanupSettings");
        public async Task<CleanupSetting?> GetCleanupSettingsAsync(ulong guildId) => await GetConn().QueryFirstOrDefaultAsync<CleanupSetting>("SELECT * FROM CleanupSettings WHERE GuildId = @guildId", new { guildId });
        public async Task SaveCleanupSettingAsync(CleanupSetting s) => await GetConn().ExecuteAsync("INSERT INTO CleanupSettings (GuildId, ChannelId) VALUES (@GuildId, @ChannelId) ON CONFLICT (GuildId) DO UPDATE SET ChannelId = @ChannelId", s);
        public async Task DeleteCleanupSettingAsync(ulong guildId) => await GetConn().ExecuteAsync("DELETE FROM CleanupSettings WHERE GuildId = @guildId", new { guildId });

        // --- GameRoom ---
        public async Task<IEnumerable<GameRoomConfig>> GetGameRoomConfigsAsync(ulong guildId) => await GetConn().QueryAsync<GameRoomConfig>("SELECT * FROM GameRoomConfigs WHERE GuildId = @guildId", new { guildId });
        public async Task<GameRoomConfig?> GetConfigByMonitorChannelAsync(ulong channelId) => await GetConn().QueryFirstOrDefaultAsync<GameRoomConfig>("SELECT * FROM GameRoomConfigs WHERE MonitorChannelId = @channelId", new { channelId });
        public async Task SaveGameRoomConfigAsync(GameRoomConfig c) => await GetConn().ExecuteAsync("INSERT INTO GameRoomConfigs (GuildId, MonitorChannelId, TargetChannelId, OriginalNameFormat) VALUES (@GuildId, @MonitorChannelId, @TargetChannelId, @OriginalNameFormat)", c);

        // --- Role ---
        public async Task<IEnumerable<RoleGiveConfig>> GetRoleGiveConfigsAsync(ulong messageId) => await GetConn().QueryAsync<RoleGiveConfig>("SELECT * FROM RoleGiveConfigs WHERE MessageId = @messageId", new { messageId });
        public async Task<RoleGiveConfig?> GetRoleGiveConfigAsync(ulong messageId, string emoji) => await GetConn().QueryFirstOrDefaultAsync<RoleGiveConfig>("SELECT * FROM RoleGiveConfigs WHERE MessageId = @messageId AND EmojiName = @emoji", new { messageId, emoji });
        public async Task SaveRoleGiveConfigAsync(RoleGiveConfig r) => await GetConn().ExecuteAsync("INSERT INTO RoleGiveConfigs (MessageId, RoleId, EmojiName) VALUES (@MessageId, @RoleId, @EmojiName)", r);

        // --- Messenger ---
        public async Task<IEnumerable<MessageTask>> GetMessageTasksByChannelAsync(ulong channelId) => await GetConn().QueryAsync<MessageTask>("SELECT * FROM MessageTasks WHERE ChannelId = @channelId", new { channelId });
        public async Task<IEnumerable<MessageTask>> GetTasksByTimeAsync(DateTime time) => await GetConn().QueryAsync<MessageTask>("SELECT * FROM MessageTasks WHERE ScheduledTime <= @time", new { time });
        public async Task SaveMessageTaskAsync(MessageTask t) => await GetConn().ExecuteAsync("INSERT INTO MessageTasks (ChannelId, Content, ScheduledTime) VALUES (@ChannelId, @Content, @ScheduledTime)", t);
        public async Task DeleteMessageTaskAsync(int id) => await GetConn().ExecuteAsync("DELETE FROM MessageTasks WHERE Id = @id", new { id });
    }
}

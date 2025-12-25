using Dapper;
using Npgsql;
using Microsoft.Extensions.Configuration;
using Discord.Data;

namespace Discord.Data
{
    public class DataService
    {
        private readonly string _connectionString;

        public DataService(IConfiguration configuration)
        {
            // Railwayの環境変数(DATABASE_URL)または設定ファイルから接続文字列を取得
            _connectionString = configuration.GetConnectionString("DefaultConnection") 
                               ?? Environment.GetEnvironmentVariable("DATABASE_URL") 
                               ?? "";
        }

        // ヘルパーメソッド：接続の作成
        private NpgsqlConnection GetConn() => new NpgsqlConnection(_connectionString);

        // --- 1. CleanupModule / TimeSignalWorker 用 ---
        
        // 全ギルドの設定取得
        public async Task<IEnumerable<CleanupSetting>> GetAllCleanupSettingsAsync() 
            => await GetConn().QueryAsync<CleanupSetting>("SELECT * FROM CleanupSettings");

        // 特定ギルドの設定取得（リスト形式：foreachでのエラー回避用）
        public async Task<IEnumerable<CleanupSetting>> GetCleanupSettingsListAsync(ulong guildId) 
            => await GetConn().QueryAsync<CleanupSetting>("SELECT * FROM CleanupSettings WHERE GuildId = @guildId", new { guildId });

        // 設定の保存（4つの引数を受け取る形式）
        public async Task SaveCleanupSettingAsync(ulong guildId, ulong channelId, int days, string type) 
        {
            const string sql = @"
                INSERT INTO CleanupSettings (GuildId, ChannelId, DaysBefore, ProtectionType) 
                VALUES (@guildId, @channelId, @days, @type) 
                ON CONFLICT (GuildId) 
                DO UPDATE SET ChannelId = @channelId, DaysBefore = @days, ProtectionType = @type";
            await GetConn().ExecuteAsync(sql, new { guildId, channelId, days, type });
        }

        // 設定の削除
        public async Task DeleteCleanupSettingAsync(ulong guildId) 
            => await GetConn().ExecuteAsync("DELETE FROM CleanupSettings WHERE GuildId = @guildId", new { guildId });


        // --- 2. GameAssistModule 用 ---

        public async Task<IEnumerable<GameRoomConfig>> GetGameRoomConfigsAsync(ulong guildId) 
            => await GetConn().QueryAsync<GameRoomConfig>("SELECT * FROM GameRoomConfigs WHERE GuildId = @guildId", new { guildId });

        public async Task<GameRoomConfig?> GetConfigByMonitorChannelAsync(ulong channelId) 
            => await GetConn().QueryFirstOrDefaultAsync<GameRoomConfig>("SELECT * FROM GameRoomConfigs WHERE MonitorChannelId = @channelId", new { channelId });

        public async Task SaveGameRoomConfigAsync(GameRoomConfig c) 
        {
            const string sql = @"
                INSERT INTO GameRoomConfigs (GuildId, MonitorChannelId, TargetChannelId, OriginalNameFormat) 
                VALUES (@GuildId, @MonitorChannelId, @TargetChannelId, @OriginalNameFormat)";
            await GetConn().ExecuteAsync(sql, c);
        }


        // --- 3. RoleModule / Program.cs 用 ---

        public async Task<IEnumerable<RoleGiveConfig>> GetRoleGiveConfigsAsync(ulong messageId) 
            => await GetConn().QueryAsync<RoleGiveConfig>("SELECT * FROM RoleGiveConfigs WHERE MessageId = @messageId", new { messageId });

        // Program.cs 等で引数2つで呼ばれる場合に対応
        public async Task<RoleGiveConfig?> GetRoleGiveConfigAsync(ulong messageId, string emoji) 
            => await GetConn().QueryFirstOrDefaultAsync<RoleGiveConfig>(
                "SELECT * FROM RoleGiveConfigs WHERE MessageId = @messageId AND EmojiName = @emoji", new { messageId, emoji });

        public async Task SaveRoleGiveConfigAsync(ulong msgId, ulong roleId, string emoji) 
        {
            const string sql = @"
                INSERT INTO RoleGiveConfigs (MessageId, RoleId, EmojiName) 
                VALUES (@msgId, @roleId, @emoji)";
            await GetConn().ExecuteAsync(sql, new { msgId, roleId, emoji });
        }


        // --- 4. MessengerModule / TimeSignalWorker 用 ---

        public async Task<IEnumerable<MessageTask>> GetMessageTasksByChannelAsync(ulong channelId) 
            => await GetConn().QueryAsync<MessageTask>("SELECT * FROM MessageTasks WHERE ChannelId = @channelId", new { channelId });

        public async Task<IEnumerable<MessageTask>> GetTasksByTimeAsync(DateTime time) 
            => await GetConn().QueryAsync<MessageTask>("SELECT * FROM MessageTasks WHERE ScheduledTime <= @time", new { time });

        public async Task SaveMessageTaskAsync(ulong channelId, string content, DateTime time) 
        {
            const string sql = @"
                INSERT INTO MessageTasks (ChannelId, Content, ScheduledTime) 
                VALUES (@channelId, @content, @time)";
            await GetConn().ExecuteAsync(sql, new { channelId, content, time });
        }

        public async Task DeleteMessageTaskAsync(int id) 
            => await GetConn().ExecuteAsync("DELETE FROM MessageTasks WHERE Id = @id", new { id });
    }
}

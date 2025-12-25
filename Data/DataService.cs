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
            // 1. 環境変数から DATABASE_URL を取得
            var url = Environment.GetEnvironmentVariable("DATABASE_URL");

            // 2. Railwayなどの postgres:// 形式を .NET 用に変換
            if (!string.IsNullOrEmpty(url) && url.Contains("://"))
            {
                try
                {
                    var uri = new Uri(url);
                    var userInfo = uri.UserInfo.Split(':');
                    
                    // Npgsql が解釈できる接続文字列に組み立て
                    _connectionString = $"Host={uri.Host};" +
                                      $"Port={uri.Port};" +
                                      $"Username={userInfo[0]};" +
                                      $"Password={userInfo[1]};" +
                                      $"Database={uri.AbsolutePath.Trim('/')};" +
                                      $"SSL Mode=Require;" +
                                      $"Trust Server Certificate=True";
                }
                catch (Exception)
                {
                    // 解析に失敗した場合はそのまま代入を試みる
                    _connectionString = url;
                }
            }
            else
            {
                // URL形式でない場合（既に Host= 形式の場合や空の場合）
                _connectionString = url ?? "";
            }
        }

        // ヘルパーメソッド：接続の作成
        private NpgsqlConnection GetConn() => new NpgsqlConnection(_connectionString);

        // --- 1. CleanupModule / TimeSignalWorker 用 ---
        public async Task<IEnumerable<CleanupSetting>> GetAllCleanupSettingsAsync() 
            => await GetConn().QueryAsync<CleanupSetting>("SELECT * FROM CleanupSettings");

        public async Task<IEnumerable<CleanupSetting>> GetCleanupSettingsListAsync(ulong guildId) 
            => await GetConn().QueryAsync<CleanupSetting>("SELECT * FROM CleanupSettings WHERE GuildId = @guildId", new { guildId });

        public async Task SaveCleanupSettingAsync(ulong guildId, ulong channelId, int days, string type) 
        {
            const string sql = @"
                INSERT INTO CleanupSettings (GuildId, ChannelId, DaysBefore, ProtectionType) 
                VALUES (@guildId, @channelId, @days, @type) 
                ON CONFLICT (GuildId) 
                DO UPDATE SET ChannelId = @channelId, DaysBefore = @days, ProtectionType = @type";
            await GetConn().ExecuteAsync(sql, new { guildId, channelId, days, type });
        }

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

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
            // Railwayの環境変数などから接続文字列を取得
            _connectionString = configuration.GetConnectionString("DefaultConnection") 
                               ?? Environment.GetEnvironmentVariable("DATABASE_URL") 
                               ?? "";
        }

        // --- 以下、エラーを解消するためのメソッド群 ---

        // CleanerModule用
        public async Task<object?> GetCleanupSettingsAsync(ulong guildId)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            return await conn.QueryFirstOrDefaultAsync("SELECT * FROM CleanupSettings WHERE GuildId = @guildId", new { guildId });
        }

        // GameAssistModule用
        public async Task<IEnumerable<object>> GetGameRoomConfigsAsync(ulong guildId)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            return await conn.QueryAsync("SELECT * FROM GameRoomConfigs WHERE GuildId = @guildId", new { guildId });
        }

        // RoleModule用
        public async Task<IEnumerable<object>> GetRoleGiveConfigsAsync(ulong guildId)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            return await conn.QueryAsync("SELECT * FROM RoleGiveConfigs WHERE GuildId = @guildId", new { guildId });
        }

        // MessengerModule用
        public async Task<IEnumerable<object>> GetMessageTasksByChannelAsync(ulong channelId)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            return await conn.QueryAsync("SELECT * FROM MessageTasks WHERE ChannelId = @channelId", new { channelId });
        }
    }
}

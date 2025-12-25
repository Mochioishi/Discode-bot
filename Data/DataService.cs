using Dapper;
using Npgsql;
using Microsoft.Extensions.Configuration;
using DiscordTimeSignal.Data;

namespace DiscordTimeSignal.Data;

public class DataService
{
    private readonly string _connectionString;

    public DataService(IConfiguration config)
    {
        // Railwayの環境変数またはappsettings.jsonから接続文字列を取得
        _connectionString = config.GetConnectionString("DefaultConnection") 
            ?? config["DATABASE_URL"]; // Railway用
        
        // PostgreSQLの snake_case を C# の PascalCase にマッピング
        DefaultTypeMap.MatchNamesWithUnderscores = true;
    }

    private NpgsqlConnection GetConnection() => new(_connectionString);

    // --- 1. CleanerModule 用 (deleteago) ---
    public async Task SaveCleanupSettingAsync(ulong guildId, ulong channelId, int days, string protect)
    {
        const string sql = @"
            INSERT INTO cleanup_settings (guild_id, channel_id, days_before, protection_type)
            VALUES (@guildId, @channelId, @days, @protect)
            ON CONFLICT (channel_id) DO UPDATE 
            SET days_before = @days, protection_type = @protect;";
        using var db = GetConnection();
        await db.ExecuteAsync(sql, new { guildId, channelId, days, protect });
    }

    public async Task<IEnumerable<dynamic>> GetAllCleanupSettingsAsync()
    {
        const string sql = "SELECT * FROM cleanup_settings;";
        using var db = GetConnection();
        return await db.QueryAsync(sql);
    }

    public async Task DeleteCleanupSettingAsync(ulong channelId)
    {
        const string sql = "DELETE FROM cleanup_settings WHERE channel_id = @channelId;";
        using var db = GetConnection();
        await db.ExecuteAsync(sql, new { channelId });
    }

    // --- 2. MessengerModule 用 (bottext) ---
    public async Task SaveMessageTaskAsync(BotMessageTask task)
    {
        const string sql = @"
            INSERT INTO scheduled_messages (id, channel_id, content, is_embed, embed_title, scheduled_time)
            VALUES (@Id, @ChannelId, @Content, @IsEmbed, @EmbedTitle, @ScheduledTime);";
        using var db = GetConnection();
        await db.ExecuteAsync(sql, task);
    }

    public async Task<IEnumerable<BotMessageTask>> GetTasksByTimeAsync(string time)
    {
        const string sql = "SELECT * FROM scheduled_messages WHERE scheduled_time = @time;";
        using var db = GetConnection();
        return await db.QueryAsync<BotMessageTask>(sql, new { time });
    }

    public async Task DeleteMessageTaskAsync(Guid id)
    {
        const string sql = "DELETE FROM scheduled_messages WHERE id = @id;";
        using var db = GetConnection();
        await db.ExecuteAsync(sql, new { id });
    }

    // --- 3. GameAssistModule 用 (prsk) ---
    public async Task SaveGameRoomConfigAsync(GameRoomConfig config)
    {
        const string sql = @"
            INSERT INTO game_room_configs (monitor_channel_id, target_channel_id, original_name_format, guild_id)
            VALUES (@MonitorChannelId, @TargetChannelId, @OriginalNameFormat, @GuildId)
            ON CONFLICT (monitor_channel_id) DO UPDATE 
            SET target_channel_id = @TargetChannelId, original_name_format = @OriginalNameFormat;";
        using var db = GetConnection();
        await db.ExecuteAsync(sql, config);
    }

    public async Task<GameRoomConfig?> GetConfigByMonitorChannelAsync(ulong channelId)
    {
        const string sql = "SELECT * FROM game_room_configs WHERE monitor_channel_id = @channelId LIMIT 1;";
        using var db = GetConnection();
        return await db.QueryFirstOrDefaultAsync<GameRoomConfig>(sql, new { channelId });
    }

    // --- 4. RoleModule 用 (rolegive) ---
    public async Task SaveRoleGiveConfigAsync(ulong messageId, ulong roleId, string emoji)
    {
        const string sql = @"
            INSERT INTO role_give_configs (message_id, role_id, emoji_name)
            VALUES (@messageId, @roleId, @emoji)
            ON CONFLICT (message_id, emoji_name) DO UPDATE SET role_id = @roleId;";
        using var db = GetConnection();
        await db.ExecuteAsync(sql, new { messageId, roleId, emoji });
    }

    public async Task<RoleGiveConfig?> GetRoleGiveConfigAsync(ulong messageId, string emoji)
    {
        const string sql = "SELECT * FROM role_give_configs WHERE message_id = @messageId AND emoji_name = @emoji;";
        using var db = GetConnection();
        return await db.QueryFirstOrDefaultAsync<RoleGiveConfig>(sql, new { messageId, emoji });
    }
}

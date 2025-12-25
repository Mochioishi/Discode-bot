using Dapper;
using Npgsql;
using DiscordTimeSignal.Data;

namespace DiscordTimeSignal.Data;

public class DataService
{
    private readonly string _connectionString;

    public DataService(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("DefaultConnection");
        DefaultTypeMap.MatchNamesWithUnderscores = true; // DBの蛇蛇文字(user_id)をC#のプロパティ(UserId)に自動変換
    }

    private NpgsqlConnection GetConnection() => new(_connectionString);

    // deleteago設定の保存
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

    // 他にも MessengerModule や GameAssistModule 用の Get/Save メソッドをここに追加していきます
}

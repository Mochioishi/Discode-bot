using Npgsql;

public static class DbInitializer
{
    public static void Initialize()
    {
        using var conn = new NpgsqlConnection(DatabaseConfig.GetConnectionString());
        conn.Open();

        using var cmd = new NpgsqlCommand();
        cmd.Connection = conn;

        // 例：プロセカ設定テーブル
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS prsk_settings (
                guild_id TEXT PRIMARY KEY,
                monitor_channel_id TEXT,
                target_channel_id TEXT,
                original_name TEXT
            );
            
            CREATE TABLE IF NOT EXISTS auto_purge_settings (
                channel_id TEXT PRIMARY KEY,
                days_ago INTEGER,
                protection_type TEXT
            );
        ";
        cmd.ExecuteNonQuery();
    }
}

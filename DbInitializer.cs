using Npgsql;

public static class DbInitializer
{
    public static void Initialize()
    {
        using var conn = new NpgsqlConnection(DbConfig.GetConnectionString());
        conn.Open();

        using var cmd = new NpgsqlCommand();
        cmd.Connection = conn;

        // まとめてテーブル作成
        cmd.CommandText = @"
            -- 1. 予約投稿
            CREATE TABLE IF NOT EXISTS scheduled_messages (
                id SERIAL PRIMARY KEY,
                channel_id TEXT NOT NULL,
                content TEXT NOT NULL,
                is_embed BOOLEAN DEFAULT FALSE,
                embed_title TEXT,
                scheduled_time TEXT NOT NULL -- hhmm
            );

            -- 2. 自動削除
            CREATE TABLE IF NOT EXISTS auto_purge_settings (
                channel_id TEXT PRIMARY KEY,
                days_ago INTEGER NOT NULL,
                protection_type TEXT NOT NULL -- None, Image, Reaction, Both
            );

            -- 3. プロセカ部屋番号監視
            CREATE TABLE IF NOT EXISTS prsk_settings (
                monitor_channel_id TEXT PRIMARY KEY,
                target_channel_id TEXT NOT NULL,
                original_name TEXT NOT NULL,
                game_type TEXT DEFAULT 'prsk'
            );

            -- 4. リアクションロール
            CREATE TABLE IF NOT EXISTS reaction_roles (
                id SERIAL PRIMARY KEY,
                message_id TEXT NOT NULL,
                role_id TEXT NOT NULL,
                emoji_name TEXT NOT NULL
            );
        ";
        cmd.ExecuteNonQuery();
        Console.WriteLine("Database tables initialized successfully.");
    }
}

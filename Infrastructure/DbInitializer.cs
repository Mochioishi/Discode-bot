using Dapper;

namespace Discord_bot.Infrastructure
{
    public static class DbInitializer
    {
        public static void Initialize(DbConfig db)
        {
            using var conn = db.GetConnection();

            // --- RoleGiveSettings テーブルの作成とカラム追加 ---
            conn.Execute(@"
                CREATE TABLE IF NOT EXISTS RoleGiveSettings (
                    MessageId BIGINT PRIMARY KEY,
                    EmojiName TEXT NOT NULL,
                    RoleId BIGINT NOT NULL,
                    GuildId BIGINT NOT NULL
                )");

            // ChannelId カラムが存在しない場合のみ追加する（PostgreSQL用）
            conn.Execute(@"
                DO $$ 
                BEGIN 
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                                   WHERE table_name='rolegivesettings' AND column_name='channelid') THEN 
                        ALTER TABLE RoleGiveSettings ADD COLUMN ChannelId BIGINT; 
                    END IF; 
                END $$;");

            // --- PrskSettings (プロセカ) ---
            conn.Execute(@"
                CREATE TABLE IF NOT EXISTS PrskSettings (
                    MonitorChannelId BIGINT PRIMARY KEY,
                    TargetChannelId BIGINT NOT NULL,
                    Template TEXT NOT NULL,
                    GuildId BIGINT NOT NULL
                )");

            // --- BotTextSchedules (予約投稿) ---
            conn.Execute(@"
                CREATE TABLE IF NOT EXISTS BotTextSchedules (
                    Id SERIAL PRIMARY KEY,
                    Text TEXT NOT NULL,
                    Title TEXT,
                    ScheduledTime TEXT NOT NULL,
                    IsEmbed BOOLEAN NOT NULL,
                    ChannelId BIGINT NOT NULL,
                    GuildId BIGINT NOT NULL
                )");

            // --- DeleteConfigs (自動削除) ---
            conn.Execute(@"
                CREATE TABLE IF NOT EXISTS DeleteConfigs (
                    ChannelId BIGINT PRIMARY KEY,
                    GuildId BIGINT NOT NULL,
                    Days INT NOT NULL,
                    ProtectType INT NOT NULL DEFAULT 0
                )");
        }
    }
}

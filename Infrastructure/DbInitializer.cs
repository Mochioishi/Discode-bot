using Dapper;
using System.Threading.Tasks;

namespace Discord_bot.Infrastructure
{
    // static を外して普通のクラスにする
    public class DbInitializer
    {
        private readonly DbConfig _db;
        public DbInitializer(DbConfig db) => _db = db;

        public async Task InitializeAsync()
        {
            using var conn = _db.GetConnection();

            // 1. RoleGiveSettings テーブルの作成
            await conn.ExecuteAsync(@"
                CREATE TABLE IF NOT EXISTS RoleGiveSettings (
                    MessageId BIGINT PRIMARY KEY,
                    EmojiName TEXT NOT NULL,
                    RoleId BIGINT NOT NULL,
                    GuildId BIGINT NOT NULL
                )");

            // 2. ChannelId カラムの追加チェック（PostgreSQL用）
            await conn.ExecuteAsync(@"
                DO $$ 
                BEGIN 
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                                   WHERE table_name='rolegivesettings' AND column_name='channelid') THEN 
                        ALTER TABLE RoleGiveSettings ADD COLUMN ChannelId BIGINT; 
                    END IF; 
                END $$;");

            // 3. その他のテーブル作成
            await conn.ExecuteAsync(@"
                CREATE TABLE IF NOT EXISTS PrskSettings (
                    MonitorChannelId BIGINT PRIMARY KEY,
                    TargetChannelId BIGINT NOT NULL,
                    Template TEXT NOT NULL,
                    GuildId BIGINT NOT NULL
                )");

            await conn.ExecuteAsync(@"
                CREATE TABLE IF NOT EXISTS BotTextSchedules (
                    Id SERIAL PRIMARY KEY,
                    Text TEXT NOT NULL,
                    Title TEXT,
                    ScheduledTime TEXT NOT NULL,
                    IsEmbed BOOLEAN NOT NULL,
                    ChannelId BIGINT NOT NULL,
                    GuildId BIGINT NOT NULL
                )");

            await conn.ExecuteAsync(@"
                CREATE TABLE IF NOT EXISTS DeleteConfigs (
                    ChannelId BIGINT PRIMARY KEY,
                    GuildId BIGINT NOT NULL,
                    Days INT NOT NULL,
                    ProtectType INT NOT NULL DEFAULT 0
                )");
        }
    }
}

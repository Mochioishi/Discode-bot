using Dapper;
using MySqlConnector;

namespace Discord_bot.Infrastructure // 名前空間をプロジェクト設定に統一
{
    public class DbInitializer
    {
        private readonly DbConfig _db;

        public DbInitializer(DbConfig db)
        {
            _db = db;
        }

        public async Task InitializeAsync()
        {
            using var conn = _db.GetConnection();

            // すべてのテーブルを MySQL 形式で作成
            const string sql = @"
                -- 1. 予約投稿用テーブル (bottext)
                CREATE TABLE IF NOT EXISTS BotTextSchedules (
                    Id INT AUTO_INCREMENT PRIMARY KEY,
                    Text TEXT NOT NULL,
                    Title VARCHAR(255),
                    ScheduledTime VARCHAR(10) NOT NULL,
                    IsEmbed BOOLEAN DEFAULT TRUE,
                    ChannelId BIGINT NOT NULL,
                    GuildId BIGINT NOT NULL
                );

                -- 2. 自動削除設定用テーブル (deleteago)
                CREATE TABLE IF NOT EXISTS DeleteConfigs (
                    ChannelId BIGINT PRIMARY KEY,
                    GuildId BIGINT NOT NULL,
                    Days INT NOT NULL,
                    ProtectType INT NOT NULL
                );

                -- 3. プロセカ監視設定用テーブル (prsk_roomid)
                CREATE TABLE IF NOT EXISTS PrskSettings (
                    MonitorChannelId BIGINT PRIMARY KEY,
                    TargetChannelId BIGINT NOT NULL,
                    Template VARCHAR(255) NOT NULL,
                    GuildId BIGINT NOT NULL
                );

                -- 4. リアクションロール用テーブル (rolegive)
                CREATE TABLE IF NOT EXISTS RoleGiveSettings (
                    MessageId BIGINT PRIMARY KEY,
                    EmojiName VARCHAR(255) NOT NULL,
                    RoleId BIGINT NOT NULL,
                    GuildId BIGINT NOT NULL
                );

                -- 5. 既存の予約削除用 (互換性維持が必要な場合)
                CREATE TABLE IF NOT EXISTS ScheduledDeletions (
                    MessageId BIGINT PRIMARY KEY,
                    ChannelId BIGINT NOT NULL,
                    DeleteAt DATETIME NOT NULL
                );";

            try
            {
                await conn.ExecuteAsync(sql);
                Console.WriteLine("[DB] --- Database initialized (Existing data preserved) ---");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DB] Initialization Error: {ex.Message}");
                throw;
            }
        }
    }
}

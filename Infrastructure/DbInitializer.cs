using Dapper;
using Npgsql;
using System;
using System.Threading.Tasks;

namespace Discord_bot.Infrastructure
{
    public class DbInitializer
    {
        private readonly DbConfig _db;
        public DbInitializer(DbConfig db) => _db = db;

        public async Task InitializeAsync()
        {
            try 
            {
                using var conn = _db.GetConnection();
                const string sql = @"
                    CREATE TABLE IF NOT EXISTS BotTextSchedules (
                        Id SERIAL PRIMARY KEY,
                        Text TEXT NOT NULL,
                        Title VARCHAR(255),
                        ScheduledTime VARCHAR(10) NOT NULL,
                        IsEmbed BOOLEAN DEFAULT TRUE,
                        ChannelId BIGINT NOT NULL,
                        GuildId BIGINT NOT NULL
                    );
                    CREATE TABLE IF NOT EXISTS DeleteConfigs (
                        ChannelId BIGINT PRIMARY KEY,
                        GuildId BIGINT NOT NULL,
                        Days INT NOT NULL,
                        ProtectType INT NOT NULL
                    );
                    CREATE TABLE IF NOT EXISTS PrskSettings (
                        MonitorChannelId BIGINT PRIMARY KEY,
                        TargetChannelId BIGINT NOT NULL,
                        Template VARCHAR(255) NOT NULL,
                        GuildId BIGINT NOT NULL
                    );
                    CREATE TABLE IF NOT EXISTS RoleGiveSettings (
                        MessageId BIGINT PRIMARY KEY,
                        EmojiName VARCHAR(255) NOT NULL,
                        RoleId BIGINT NOT NULL,
                        GuildId BIGINT NOT NULL
                    );";
                await conn.ExecuteAsync(sql);
                Console.WriteLine("[DB] Initialized successfully for PostgreSQL.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DB Error] {ex.Message}");
            }
        }
    }
}

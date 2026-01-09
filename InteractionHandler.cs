using Npgsql;
using System;

namespace DiscordBot.Infrastructure
{
    public static class DbInitializer
    {
        public static void Initialize()
        {
            using var conn = new NpgsqlConnection(DbConfig.GetConnectionString());
            conn.Open();
            using var cmd = new NpgsqlCommand();
            cmd.Connection = conn;

            cmd.CommandText = @"
                -- 1. 自動削除 (Role/Global用)
                CREATE TABLE IF NOT EXISTS ""ScheduledDeletions"" (
                    ""MessageId"" BIGINT PRIMARY KEY,
                    ""ChannelId"" BIGINT NOT NULL,
                    ""DeleteAt"" TIMESTAMP WITH TIME ZONE NOT NULL
                );

                -- 2. リアクションロール
                CREATE TABLE IF NOT EXISTS ""ReactionRoles"" (
                    ""MessageId"" BIGINT,
                    ""Emoji"" TEXT,
                    ""RoleId"" BIGINT,
                    PRIMARY KEY (""MessageId"", ""Emoji"")
                );

                -- 3. 予約投稿
                CREATE TABLE IF NOT EXISTS ""BotTextSchedules"" (
                    ""Id"" SERIAL PRIMARY KEY,
                    ""Text"" TEXT NOT NULL,
                    ""Title"" TEXT,
                    ""ScheduledTime"" TEXT NOT NULL,
                    ""ShowTime"" BOOLEAN DEFAULT TRUE
                );

                -- 4. チャンネル自動掃除 (deleteago)
                CREATE TABLE IF NOT EXISTS ""AutoPurgeSettings"" (
                    ""ChannelId"" TEXT PRIMARY KEY,
                    ""DaysAgo"" INTEGER NOT NULL,
                    ""ProtectionType"" TEXT NOT NULL
                );

                -- 5. プロセカ部屋番号監視
                CREATE TABLE IF NOT EXISTS ""PrskSettings"" (
                    ""MonitorChannelId"" TEXT PRIMARY KEY,
                    ""TargetChannelId"" TEXT NOT NULL,
                    ""Template"" TEXT NOT NULL
                );";

            cmd.ExecuteNonQuery();
            Console.WriteLine("--- Database Tables Initialized (Simple & Clean) ---");
        }
    }
}

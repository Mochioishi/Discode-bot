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

            // すべてのテーブルを「存在しない時だけ作る」設定に変更しました
            cmd.CommandText = @"
                -- 1. 自動削除テーブル
                CREATE TABLE IF NOT EXISTS ""ScheduledDeletions"" (
                    ""MessageId"" BIGINT PRIMARY KEY,
                    ""ChannelId"" BIGINT NOT NULL,
                    ""DeleteAt"" TIMESTAMP WITH TIME ZONE NOT NULL
                );

                -- 2. リアクションロールテーブル
                CREATE TABLE IF NOT EXISTS ""ReactionRoles"" (
                    ""MessageId"" BIGINT,
                    ""Emoji"" TEXT,
                    ""RoleId"" BIGINT,
                    PRIMARY KEY (""MessageId"", ""Emoji"")
                );

                -- 3. Botテキスト予約投稿用テーブル (BotTextSchedules)
                -- 前回の統合版に合わせ、ChannelId カラムを含む最新構成で作成します
                CREATE TABLE IF NOT EXISTS ""BotTextSchedules"" (
                    ""Id"" SERIAL PRIMARY KEY,
                    ""Text"" TEXT NOT NULL,
                    ""Title"" TEXT,
                    ""ScheduledTime"" TEXT NOT NULL,
                    ""ShowTime"" BOOLEAN DEFAULT TRUE,
                    ""ChannelId"" TEXT NOT NULL
                );

                -- 4. プロセカ監視設定用テーブル
                CREATE TABLE IF NOT EXISTS ""PrskSettings"" (
                    ""MonitorChannelId"" TEXT PRIMARY KEY,
                    ""TargetChannelId"" TEXT NOT NULL,
                    ""Template"" TEXT NOT NULL
                );

                -- 5. 自動掃除設定用テーブル (deleteago)
                CREATE TABLE IF NOT EXISTS ""AutoPurgeSettings"" (
                    ""ChannelId"" TEXT PRIMARY KEY,
                    ""DaysAgo"" INTEGER NOT NULL,
                    ""ProtectionType"" TEXT NOT NULL
                );";

            cmd.ExecuteNonQuery();
            Console.WriteLine("--- Database initialized (Existing data preserved) ---");
        }
    }
}

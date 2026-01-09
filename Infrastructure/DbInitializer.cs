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
                -- 1. 自動削除テーブル
                DROP TABLE IF EXISTS scheduleddeletions CASCADE;
                DROP TABLE IF EXISTS ""ScheduledDeletions"" CASCADE;

                CREATE TABLE ""ScheduledDeletions"" (
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

                -- 3. Botテキスト保存用テーブル
                -- カラム名を設計（Text, Title, ShowTime）に合わせて一新します
                DROP TABLE IF EXISTS bottexts CASCADE;
                DROP TABLE IF EXISTS ""BotTexts"" CASCADE;

                CREATE TABLE ""BotTexts"" (
                    ""Text"" TEXT,            -- 本文
                    ""Title"" TEXT,           -- タイトル
                    ""ShowTime"" BOOLEAN DEFAULT FALSE -- 時刻表示フラグ
                );";

            cmd.ExecuteNonQuery();
            Console.WriteLine("--- CRITICAL: TABLES RE-CREATED FROM SCRATCH ---");
        }
    }
}

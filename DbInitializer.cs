using Npgsql;
using System;

public static class DbInitializer
{
    public static void Initialize()
    {
        using var conn = new NpgsqlConnection(DbConfig.GetConnectionString());
        conn.Open();

        using var cmd = new NpgsqlCommand();
        cmd.Connection = conn;

        // 【強行突破】古い残骸を消し、Workerが探しているテーブルを正確に作成する
        cmd.CommandText = @"
            -- 既存のテーブルがあれば一度消去（幽霊テーブル対策）
            DROP TABLE IF EXISTS scheduleddeletions;
            DROP TABLE IF EXISTS ""ScheduledDeletions"";
            DROP TABLE IF EXISTS reaction_roles;
            DROP TABLE IF EXISTS ""ReactionRoles"";

            -- Worker.cs が必要としているテーブル
            CREATE TABLE ""ScheduledDeletions"" (
                ""MessageId"" BIGINT PRIMARY KEY,
                ""ChannelId"" BIGINT NOT NULL,
                ""DeleteAt"" TIMESTAMP WITH TIME ZONE NOT NULL
            );

            -- RoleModule/InteractionHandler が必要としているテーブル
            CREATE TABLE ""ReactionRoles"" (
                ""MessageId"" BIGINT,
                ""Emoji"" TEXT,
                ""RoleId"" BIGINT,
                PRIMARY KEY (""MessageId"", ""Emoji"")
            );

            -- 他の既存テーブル（必要であれば残す）
            CREATE TABLE IF NOT EXISTS scheduled_messages (
                id SERIAL PRIMARY KEY,
                channel_id TEXT NOT NULL,
                content TEXT NOT NULL,
                scheduled_time TEXT NOT NULL
            );
        ";

        cmd.ExecuteNonQuery();
        
        // 【成功の目印】ログ出力を変えます
        Console.WriteLine("--- CRITICAL: TABLES RE-CREATED FROM SCRATCH ---");
    }
}

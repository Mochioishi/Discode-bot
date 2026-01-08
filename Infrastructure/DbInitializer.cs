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

        // 強制リセット
        cmd.CommandText = @"
            DROP TABLE IF EXISTS scheduleddeletions;
            DROP TABLE IF EXISTS ""ScheduledDeletions"";

            CREATE TABLE ""ScheduledDeletions"" (
                ""MessageId"" BIGINT PRIMARY KEY,
                ""ChannelId"" BIGINT NOT NULL,
                ""DeleteAt"" TIMESTAMP WITH TIME ZONE NOT NULL
            );

            CREATE TABLE IF NOT EXISTS ""ReactionRoles"" (
                ""MessageId"" BIGINT,
                ""Emoji"" TEXT,
                ""RoleId"" BIGINT,
                PRIMARY KEY (""MessageId"", ""Emoji"")
            );";

        cmd.ExecuteNonQuery();
        Console.WriteLine("--- CRITICAL: TABLES RE-CREATED FROM SCRATCH ---");
    }
}

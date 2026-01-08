using Npgsql;
using System;
using System.Threading.Tasks;

public class DatabaseInitializer
{
    private readonly string _connectionString;

    public DatabaseInitializer(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task InitializeAsync()
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        // 幽霊テーブルを物理的に破壊して、一文字も狂いなく新築する命令
        var sql = @"
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

        using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
        
        // 【ここが目印！】このログが出れば「新しいコード」が動いた証拠です
        Console.WriteLine("--- CRITICAL: TABLES RE-CREATED FROM SCRATCH ---");
    }
}

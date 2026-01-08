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

        // 強制リセット命令：古い小文字のテーブルを消し、大文字混じりの正しい名前で作る
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
        Console.WriteLine("CRITICAL: Database tables RE-CREATED successfully.");
    }
}

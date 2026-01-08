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

        // テーブル名とカラム名を \" で囲むことで PostgreSQL の大文字小文字問題を回避
        var sql = @"
            CREATE TABLE IF NOT EXISTS ""ScheduledDeletions"" (
                ""MessageId"" BIGINT PRIMARY KEY,
                ""ChannelId"" BIGINT NOT NULL,
                ""DeleteAt"" TIMESTAMP WITH TIME ZONE NOT NULL
            );";

        using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
        Console.WriteLine("Database tables initialized successfully.");
    }
}

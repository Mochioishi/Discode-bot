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

        // 1. 古いテーブル（小文字や中途半端なもの）を一度完全に消去します
        // 2. その後、Worker が探している正確な名前で作り直します
        var sql = @"
            DROP TABLE IF EXISTS ""ScheduledDeletions"";
            DROP TABLE IF EXISTS scheduleddeletions;

            CREATE TABLE ""ScheduledDeletions"" (
                ""MessageId"" BIGINT PRIMARY KEY,
                ""ChannelId"" BIGINT NOT NULL,
                ""DeleteAt"" TIMESTAMP WITH TIME ZONE NOT NULL
            );";

        using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
        Console.WriteLine("Database tables RE-CREATED successfully.");
    }
}

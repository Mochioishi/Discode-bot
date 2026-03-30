using System.Data;
using Npgsql;

namespace DiscordBot.Database;

public class DbService
{
    private readonly string _connectionString;

    public DbService()
    {
        // Railway環境変数から取得
        _connectionString = Environment.GetEnvironmentVariable("DATABASE_URL") 
            ?? throw new Exception("DATABASE_URL is not set.");
    }

    public IDbConnection GetConnection() => new NpgsqlConnection(_connectionString);
}

using System.Data;
using Npgsql;

namespace DiscordBot.Database;

public class DbService
{
    private readonly string _connectionString;
    public DbService() => _connectionString = Environment.GetEnvironmentVariable("DATABASE_URL") ?? "";
    public IDbConnection GetConnection() => new NpgsqlConnection(_connectionString);
}

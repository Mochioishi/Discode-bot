using MySqlConnector;
using Microsoft.Extensions.Configuration;

namespace Discord_bot.Infrastructure // ← ここを「Discord_bot」に統一
{
    public class DbConfig
    {
        private readonly string _connectionString;

        public DbConfig(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("Default") 
                               ?? "Server=localhost;Database=discord_bot;Uid=root;Pwd=password;";
        }

        public MySqlConnection GetConnection() => new MySqlConnection(_connectionString);
    }
}

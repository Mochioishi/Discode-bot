using MySqlConnector;
using Microsoft.Extensions.Configuration;

namespace Discord_bot.Infrastructure
{
    public class DbConfig
    {
        private readonly string _connectionString;

        public DbConfig(IConfiguration configuration)
        {
            var rawUrl = configuration["DATABASE_URL"];

            if (!string.IsNullOrEmpty(rawUrl) && rawUrl.StartsWith("mysql://"))
            {
                var uri = new Uri(rawUrl);
                var userInfo = uri.UserInfo.Split(':');
                _connectionString = $"Server={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.Trim('/')};Uid={userInfo[0]};Pwd={userInfo[1]};SSL Mode=Required;AllowPublicKeyRetrieval=True;Charset=utf8mb4;";
                Console.WriteLine($"[DB Config] Host: {uri.Host} Parsed.");
            }
            else
            {
                _connectionString = configuration.GetConnectionString("Default") ?? "";
            }
        }

        public MySqlConnection GetConnection() => new MySqlConnection(_connectionString);
    }
}

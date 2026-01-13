using MySqlConnector;
using Microsoft.Extensions.Configuration;
using System;

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
                // mysql://user:password@host:port/database 形式を変換
                var uri = new Uri(rawUrl);
                var userInfo = uri.UserInfo.Split(':');
                var user = userInfo[0];
                var password = userInfo.Length > 1 ? userInfo[1] : "";
                var host = uri.Host;
                var port = uri.Port;
                var database = uri.AbsolutePath.Trim('/');

                // Render/Railway等では SSL Mode=Required が必要な場合が多いです
                _connectionString = $"Server={host};Port={port};Database={database};Uid={user};Pwd={password};SSL Mode=Required;AllowPublicKeyRetrieval=True;";
            }
            else
            {
                _connectionString = configuration.GetConnectionString("Default") ?? "";
            }
        }

        public MySqlConnection GetConnection() => new MySqlConnection(_connectionString);
    }
}

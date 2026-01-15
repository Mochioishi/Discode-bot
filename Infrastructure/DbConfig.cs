using Npgsql;
using System;

namespace Discord_bot.Infrastructure
{
    public class DbConfig
    {
        public string GetConnectionString()
        {
            var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
            if (string.IsNullOrEmpty(databaseUrl)) return null;

            var uri = new Uri(databaseUrl);
            var userInfo = uri.UserInfo.Split(':');

            return new NpgsqlConnectionStringBuilder
            {
                Host = uri.Host,
                Port = uri.Port,
                Username = userInfo[0],
                Password = userInfo[1],
                Database = uri.LocalPath.TrimStart('/'),
                SslMode = SslMode.Require,
                TrustServerCertificate = true
            }.ToString();
        }

        public NpgsqlConnection GetConnection() => new NpgsqlConnection(GetConnectionString());
    }
}

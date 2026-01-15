using Npgsql;
using System;

namespace Discord_bot.Infrastructure
{
    public class DbConfig
    {
        public string GetConnectionString()
        {
            var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");

            // デバッグ用ログ：URLが空でないか確認
            if (string.IsNullOrEmpty(databaseUrl))
            {
                Console.WriteLine("[DB Error] DATABASE_URL is null or empty. Please check Railway Variables.");
                return null;
            }

            try
            {
                // postgres://user:pass@host:port/db 形式を解析
                var uri = new Uri(databaseUrl);
                var userInfo = uri.UserInfo.Split(':');

                var builder = new NpgsqlConnectionStringBuilder
                {
                    Host = uri.Host,
                    Port = uri.Port,
                    Username = userInfo[0],
                    Password = userInfo[1],
                    Database = uri.LocalPath.TrimStart('/'),
                    SslMode = SslMode.Require,
                    TrustServerCertificate = true
                };

                return builder.ToString();
            }
            catch (Exception ex)
            {
                // 何が原因でURIエラーになっているか詳細を出力
                Console.WriteLine($"[DB Error] URI Parsing Failed. URL length: {databaseUrl.Length}");
                Console.WriteLine($"[DB Error] Details: {ex.Message}");
                return null;
            }
        }

        public NpgsqlConnection GetConnection() 
        {
            var connectionString = GetConnectionString();
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException("Connection string could not be generated.");
            }
            return new NpgsqlConnection(connectionString);
        }
    }
}

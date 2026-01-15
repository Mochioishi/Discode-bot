using Npgsql;
using System;

namespace DiscordBot.Infrastructure
{
    public static class DbConfig
    {
        public static string GetConnectionString()
        {
            // Railwayの環境変数から DATABASE_URL を取得
            var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");

            if (string.IsNullOrEmpty(databaseUrl))
            {
                // ローカル開発環境などで DATABASE_URL がない場合のフォールバック（必要に応じて）
                return "Host=localhost;Database=discord_bot;Username=postgres;Password=password";
            }

            try
            {
                // postgres://user:password@host:port/database 形式を解析
                var uri = new Uri(databaseUrl);
                var userInfo = uri.UserInfo.Split(':');

                var builder = new NpgsqlConnectionStringBuilder
                {
                    Host = uri.Host,
                    Port = uri.Port,
                    Username = userInfo[0],
                    Password = userInfo[1],
                    Database = uri.LocalPath.TrimStart('/'),
                    SslMode = SslMode.Require, // RailwayのPostgreSQLはSSL必須
                    TrustServerCertificate = true // 証明書エラーを回避
                };

                return builder.ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing DATABASE_URL: {ex.Message}");
                return null;
            }
        }

        // NpgsqlConnectionを返すヘルパーメソッド
        public static NpgsqlConnection GetConnection()
        {
            return new NpgsqlConnection(GetConnectionString());
        }
    }
}

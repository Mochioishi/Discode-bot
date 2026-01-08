using Npgsql;
using System;

public static class DatabaseConfig
{
    public static string GetConnectionString()
    {
        // Railwayの環境変数 DATABASE_URL を取得
        var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");

        if (string.IsNullOrEmpty(databaseUrl))
        {
            // ローカル開発用
            return "Host=localhost;Username=postgres;Password=password;Database=discord_bot";
        }

        // すでに環境変数を Host=... の形式に設定しているため、パースせずそのまま使います
        try
        {
            var builder = new NpgsqlConnectionStringBuilder(databaseUrl);

            // Railway内部接続用にSSLを無効化（これが一番安定します）
            builder.SslMode = SslMode.Disable;
            builder.TrustServerCertificate = true;

            return builder.ToString();
        }
        catch
        {
            // もしパースに失敗しても、最低限そのまま返す
            return databaseUrl;
        }
    }
}

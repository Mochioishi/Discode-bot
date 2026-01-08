using Npgsql;
using System;

public static class DbConfig
{
    public static string GetConnectionString()
    {
        var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");

        if (string.IsNullOrEmpty(databaseUrl))
        {
            // ローカル開発用（必要に応じて書き換えてください）
            return "Host=localhost;Username=postgres;Password=password;Database=discord_bot";
        }

        try
        {
            // Railwayの環境変数（Host=...形式）をそのまま読み込む
            var builder = new NpgsqlConnectionStringBuilder(databaseUrl);
            
            // 内部接続用にSSLを無効化し、安定性を高める
            builder.SslMode = SslMode.Disable;
            builder.TrustServerCertificate = true;

            return builder.ToString();
        }
        catch
        {
            return databaseUrl;
        }
    }
}

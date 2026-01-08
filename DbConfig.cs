using Npgsql;
using System;

public static class DbConfig
{
    public static string GetConnectionString()
    {
        var url = Environment.GetEnvironmentVariable("DATABASE_URL");
        if (string.IsNullOrEmpty(url)) return "";

        // RailwayのURL形式 (postgresql://...) を Npgsql が解釈できる形式に変換します
        try
        {
            // URLが "postgresql://" で始まる場合、NpgsqlConnectionStringBuilder が自動でパースします
            var builder = new NpgsqlConnectionStringBuilder(url)
            {
                // Railway内部接続では SSL を Disable に設定すると安定します
                SslMode = SslMode.Disable,
                TrustServerCertificate = true,
                Pooling = true,
                // タイムアウトを少し伸ばして接続を安定させます
                CommandTimeout = 30,
                InternalCommandTimeout = 30
            };

            return builder.ToString();
        }
        catch (Exception ex)
        {
            // パースに失敗した場合はログを出力
            Console.WriteLine($"[DbConfig Error] Failed to parse DATABASE_URL: {ex.Message}");
            
            // もしURLが既に 'Host=...;Username=...' の形式ならそのまま返す
            if (url.Contains("Host=") || url.Contains("Server=")) return url;
            
            return "";
        }
    }
}

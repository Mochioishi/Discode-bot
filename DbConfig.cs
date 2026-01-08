using Npgsql;
using System;

public static class DbConfig
{
    public static string GetConnectionString()
    {
        var url = Environment.GetEnvironmentVariable("DATABASE_URL");
        
        // URLが空、または "postgresql://" で始まらない場合は、安全のために空文字を返す
        if (string.IsNullOrEmpty(url) || !url.StartsWith("postgresql://"))
        {
            Console.WriteLine("[Critical] DATABASE_URL is invalid or missing.");
            return ""; 
        }

        try
        {
            var builder = new NpgsqlConnectionStringBuilder(url)
            {
                SslMode = SslMode.Disable,
                TrustServerCertificate = true,
                Pooling = true
            };
            return builder.ToString();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Critical] Failed to parse DATABASE_URL: {ex.Message}");
            return ""; // パースに失敗しても、プログラムを落とさないために空文字を返す
        }
    }
}

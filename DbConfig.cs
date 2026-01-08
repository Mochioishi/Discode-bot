using Npgsql;
using System;

public static class DbConfig
{
    public static string GetConnectionString()
    {
        // 1. まずは Railway で設定した DATABASE_URL を取得
        var url = Environment.GetEnvironmentVariable("DATABASE_URL");
        var pass = Environment.GetEnvironmentVariable("PGPASSWORD");

        if (string.IsNullOrEmpty(url)) return "";

        try
        {
            // 2. Npgsqlのビルダーを使ってパース
            var builder = new NpgsqlConnectionStringBuilder(url);

            // 3. 【重要】もしURLからパスワードが読み取れていなければ、
            // 個別の PGPASSWORD 変数から直接セットする
            if (string.IsNullOrEmpty(builder.Password) && !string.IsNullOrEmpty(pass))
            {
                builder.Password = pass;
            }

            // 4. 接続の安定化設定（これらは上書きされても安全な設定です）
            builder.SslMode = SslMode.Disable;
            builder.TrustServerCertificate = true;

            return builder.ToString();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DbConfig Error] {ex.Message}");
            return url;
        }
    }
}

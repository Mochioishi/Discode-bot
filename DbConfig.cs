using Npgsql;
using System;

public static class DbConfig
{
    public static string GetConnectionString()
    {
        // Railwayから渡される postgresql://... の形式を直接取得
        var url = Environment.GetEnvironmentVariable("DATABASE_URL");
        
        if (string.IsNullOrEmpty(url)) 
            throw new Exception("DATABASE_URL is not set.");

        // 手動でパースせず、NpgsqlConnectionStringBuilder に直接流し込むのが最も安全
        var builder = new NpgsqlConnectionStringBuilder(url)
        {
            // Railway内部接続（postgres.railway.internal）の場合、SSLが原因で認証エラーになることがある
            // そのため、SSLを無効化しつつ証明書を信頼する設定を上書きする
            SslMode = SslMode.Disable, 
            TrustServerCertificate = true,
            Pooling = true
        };

        return builder.ToString();
    }
}

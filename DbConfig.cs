using Npgsql;
using System;

public static class DbConfig
{
    public static string GetConnectionString()
    {
        var url = Environment.GetEnvironmentVariable("DATABASE_URL");
        if (string.IsNullOrEmpty(url)) throw new Exception("DATABASE_URL is not set.");

        // 【重要】手動でパース（分割）せず、ライブラリに任せる
        // これにより、パスワードに記号が含まれていても自動で正しく処理されます
        var builder = new NpgsqlConnectionStringBuilder(url);

        // Railway内部ネットワークでの接続安定化
        builder.SslMode = SslMode.Disable; 
        builder.TrustServerCertificate = true;
        builder.Pooling = true;

        return builder.ToString();
    }
}

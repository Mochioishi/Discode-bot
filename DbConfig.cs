using Npgsql;
using System;

public static class DbConfig
{
    public static string GetConnectionString()
    {
        var url = Environment.GetEnvironmentVariable("DATABASE_URL");
        if (string.IsNullOrEmpty(url)) throw new Exception("DATABASE_URL is not set.");

        // 手動パース（Uri userInfoなど）をせず、コンストラクタに直接URLを入れる
        // これにより、特殊文字が含まれていてもNpgsqlが正しく処理します
        var builder = new NpgsqlConnectionStringBuilder(url);

        // Railway内部接続で最も安定する設定
        builder.SslMode = SslMode.Disable; 
        builder.TrustServerCertificate = true;
        builder.Pooling = true;

        return builder.ToString();
    }
}

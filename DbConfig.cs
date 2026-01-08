using Npgsql;
using System;

public static class DbConfig
{
    public static string GetConnectionString()
    {
        // 1. 環境変数からURLを取得
        var url = Environment.GetEnvironmentVariable("DATABASE_URL");
        if (string.IsNullOrEmpty(url)) throw new Exception("DATABASE_URL is not set.");

        // 2. 【重要】手動でUriクラスを使って分割せず、そのままBuilderに渡す
        // 手動パースは特殊文字やエンコードで認証失敗(28P01)の原因になります
        var builder = new NpgsqlConnectionStringBuilder(url);

        // 3. Railway内部接続用の微調整
        // 内部ネットワーク(internal)ではSSLをDisableにしないと認証エラーになる場合があります
        builder.SslMode = SslMode.Disable; 
        builder.TrustServerCertificate = true;
        builder.Pooling = true;

        return builder.ToString();
    }
}

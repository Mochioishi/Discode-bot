using Npgsql;
using System;

public static class DbConfig
{
    public static string GetConnectionString()
    {
        var url = Environment.GetEnvironmentVariable("DATABASE_URL");
        if (string.IsNullOrEmpty(url)) return "";

        try
        {
            // URL形式(postgresql://)でもHost形式でも、一旦ビルダーに読み込ませる
            var builder = new NpgsqlConnectionStringBuilder(url);

            // 【重要】パスワードがもし空なら、直接環境変数から再セットを試みる
            if (string.IsNullOrEmpty(builder.Password))
            {
                // ここはPostgres側の変数名に合わせています
                builder.Password = Environment.GetEnvironmentVariable("PGPASSWORD");
            }

            // Railway内部接続用の安定設定
            builder.SslMode = SslMode.Disable;
            builder.TrustServerCertificate = true;

            return builder.ToString();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DbConfig Error] {ex.Message}");
            return url; // 失敗したら元の文字列をそのまま返して勝負する
        }
    }
}

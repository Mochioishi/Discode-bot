using Npgsql;

namespace DiscordTimeSignal.Data;

public static class DbConfig
{
    public static string GetConnectionString()
    {
        // 1. 公開用URL (DATABASE_PUBLIC_URL) を最優先で取得する
        var url = Environment.GetEnvironmentVariable("DATABASE_PUBLIC_URL") 
                  ?? Environment.GetEnvironmentVariable("DATABASE_URL");

        if (string.IsNullOrEmpty(url))
        {
            throw new Exception("接続用URLが設定されていません。RailwayのVariablesを確認してください。");
        }

        try
        {
            // postgresql:// (lあり) を postgres:// に変換して Uri クラスで解析可能にする
            var normalizedUrl = url.Replace("postgresql://", "postgres://");
            
            if (normalizedUrl.StartsWith("postgres://"))
            {
                var uri = new Uri(normalizedUrl);
                var userInfo = uri.UserInfo.Split(':');
                var user = userInfo[0];
                var password = userInfo.Length > 1 ? userInfo[1] : "";

                // SSL Mode と証明書信頼設定を強制。Poolingを有効にして安定させる
                return $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.Trim('/')};Username={user};Password={password};SSL Mode=Require;Trust Server Certificate=true;Pooling=true;";
            }
        }
        catch
        {
            // 解析に失敗した場合のバックアップロジック
            return url.Contains("?") ? $"{url}&sslmode=Require" : $"{url}?sslmode=Require";
        }

        return url;
    }
}

using Npgsql;

namespace DiscordTimeSignal.Data;

public static class DbConfig
{
    public static string GetConnectionString()
    {
        var url = Environment.GetEnvironmentVariable("DATABASE_URL");

        if (string.IsNullOrEmpty(url))
        {
            throw new Exception("環境変数 DATABASE_URL が設定されていません。");
        }

        try
        {
            // postgresql:// (lあり) を postgres:// (lなし) に置換して Uri クラスで解析しやすくする
            var normalizedUrl = url.Replace("postgresql://", "postgres://");
            
            if (normalizedUrl.StartsWith("postgres://"))
            {
                var uri = new Uri(normalizedUrl);
                var userInfo = uri.UserInfo.Split(':');
                var user = userInfo[0];
                var password = userInfo.Length > 1 ? userInfo[1] : "";

                // Railway接続に必須な SSL 設定をすべて詰め込んだ接続文字列を生成
                return $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.Trim('/')};Username={user};Password={password};SSL Mode=Require;Trust Server Certificate=true;Pooling=true;";
            }
        }
        catch
        {
            // 解析に失敗した場合は、SSL設定を末尾に足して返す
            return url.Contains("?") ? $"{url}&sslmode=Require" : $"{url}?sslmode=Require";
        }

        return url;
    }
}

using Npgsql;

namespace DiscordTimeSignal.Data;

public static class DbConfig
{
    public static string GetConnectionString()
    {
        var url = Environment.GetEnvironmentVariable("DATABASE_URL");

        if (string.IsNullOrEmpty(url))
        {
            throw new Exception("DATABASE_URL が設定されていません。");
        }

        // 修正：postgresql:// (lあり) にも対応するように変更
        if (url.StartsWith("postgres://") || url.StartsWith("postgresql://"))
        {
            // postgresql:// を postgres:// に統一して解析しやすくする
            var normalizedUrl = url.Replace("postgresql://", "postgres://");
            var uri = new Uri(normalizedUrl);
            var userInfo = uri.UserInfo.Split(':');
            var user = userInfo[0];
            var password = userInfo.Length > 1 ? userInfo[1] : "";

            // Railwayの接続に必須な設定を付与
            return $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.Trim('/')};Username={user};Password={password};SSL Mode=Require;Trust Server Certificate=true;";
        }

        return url;
    }
}

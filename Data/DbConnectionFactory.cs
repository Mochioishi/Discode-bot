using Npgsql;

namespace DiscordTimeSignal.Data;

public static class DbConfig
{
    public static string GetConnectionString()
    {
        // 1. DATABASE_URL を最優先で取得
        var url = Environment.GetEnvironmentVariable("DATABASE_URL");

        if (string.IsNullOrEmpty(url))
        {
            throw new Exception("DATABASE_URL が設定されていません。RailwayのVariablesを確認してください。");
        }

        // 2. postgres:// 形式を Npgsql 用に整形
        if (url.StartsWith("postgres://"))
        {
            var uri = new Uri(url);
            var userInfo = uri.UserInfo.Split(':');
            var user = userInfo[0];
            var password = userInfo.Length > 1 ? userInfo[1] : "";

            // Railwayの接続に必須な設定を強制付与
            return $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.Trim('/')};Username={user};Password={password};SSL Mode=Require;Trust Server Certificate=true;";
        }

        return url;
    }
}

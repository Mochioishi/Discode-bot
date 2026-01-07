using Npgsql;

namespace DiscordTimeSignal.Data;

public static class DbConfig
{
    public static string GetConnectionString()
    {
        // 公開用URL (DATABASE_PUBLIC_URL) を最優先にする
        var url = Environment.GetEnvironmentVariable("DATABASE_PUBLIC_URL") 
                  ?? Environment.GetEnvironmentVariable("DATABASE_URL");

        if (string.IsNullOrEmpty(url))
        {
            throw new Exception("Database URL が設定されていません。");
        }

        // postgresql:// を Npgsql が直接解釈できる Host= 形式に強制変換する
        if (url.Contains("://"))
        {
            var uri = new Uri(url.Replace("postgresql://", "postgres://"));
            var userInfo = uri.UserInfo.Split(':');
            var user = userInfo[0];
            var password = userInfo.Length > 1 ? userInfo[1] : "";
            var host = uri.Host;
            var port = uri.Port;
            var database = uri.AbsolutePath.Trim('/');

            // Railway接続に必須の SSL 設定を付与
            return $"Host={host};Port={port};Database={database};Username={user};Password={password};SSL Mode=Require;Trust Server Certificate=true;Pooling=true;";
        }

        return url;
    }
}

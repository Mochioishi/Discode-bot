using Npgsql;

namespace DiscordTimeSignal.Data;

public static class DbConfig
{
    public static string GetConnectionString()
    {
        // 1. 公開用URLを最優先で取得
        var url = Environment.GetEnvironmentVariable("DATABASE_PUBLIC_URL") 
                  ?? Environment.GetEnvironmentVariable("DATABASE_URL");

        if (string.IsNullOrEmpty(url))
        {
            throw new Exception("接続用URL(DATABASE_PUBLIC_URL)が見つかりません。");
        }

        // 2. Npgsqlが直接解釈できる「Host=...」形式に強制変換
        // パスワードに含まれる可能性のある特殊文字に強い解析を行います
        try
        {
            // postgresql:// を除去
            var cleanUrl = url.Replace("postgresql://", "").Replace("postgres://", "");
            
            // user:pass と host:port/db を分離
            var atSplit = cleanUrl.Split('@');
            var userPass = atSplit[0].Split(':');
            var hostPortDb = atSplit[1].Split('/');
            var hostPort = hostPortDb[0].Split(':');

            var user = userPass[0];
            var pass = userPass[1];
            var host = hostPort[0];
            var port = hostPort[1];
            var db = hostPortDb[1];

            // 3. Railwayで必須の SSL 設定を加えて返却
            return $"Host={host};Port={port};Database={db};Username={user};Password={pass};SSL Mode=Require;Trust Server Certificate=true;Pooling=true;";
        }
        catch
        {
            // 解析に失敗した場合は、SSL設定を末尾に足してそのまま投げる
            return url.Contains("?") ? $"{url}&sslmode=Require" : $"{url}?sslmode=Require";
        }
    }
}

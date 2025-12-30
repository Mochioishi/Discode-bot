using Npgsql;

namespace DiscordTimeSignal.Data;

public static class DbConnectionFactory
{
    public static string GetConnectionString()
    {
        // 1. Railwayの環境変数を取得
        var url = Environment.GetEnvironmentVariable("DATABASE_URL");

        if (string.IsNullOrEmpty(url))
        {
            throw new Exception("DATABASE_URL が設定されていません。RailwayのVariablesを確認してください。");
        }

        // 2. postgres:// 形式を Npgsql が好む形式に正規化
        // 稀に Npgsql のバージョンによって URL を直接読めない場合があるため、ここで整形します
        if (url.StartsWith("postgres://"))
        {
            var uri = new Uri(url);
            var userInfo = uri.UserInfo.Split(':');
            var user = userInfo[0];
            var password = userInfo.Length > 1 ? userInfo[1] : "";

            // SSL設定を強制的に付与（Railwayでは必須）
            return $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.Trim('/')};Username={user};Password={password};SSL Mode=Require;Trust Server Certificate=true;";
        }

        return url;
    }

    public static NpgsqlConnection CreateConnection()
    {
        return new NpgsqlConnection(GetConnectionString());
    }
}

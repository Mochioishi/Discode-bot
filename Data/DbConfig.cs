using Npgsql;

namespace DiscordTimeSignal.Data;

public static class DbConfig
{
    public static string GetConnectionString()
    {
        // 1. 公開用URLを優先的にチェック
        var url = Environment.GetEnvironmentVariable("DATABASE_PUBLIC_URL") 
                  ?? Environment.GetEnvironmentVariable("DATABASE_URL");

        if (string.IsNullOrEmpty(url))
        {
            throw new Exception("DATABASE_PUBLIC_URL または DATABASE_URL が設定されていません。");
        }

        try
        {
            // 2. postgresql:// 形式を解析
            // Uriクラスはパスワードに特殊記号があると失敗しやすいため、文字列操作で確実に抽出します
            var cleanUrl = url.Replace("postgresql://", "").Replace("postgres://", "");
            
            // ユーザー情報(@の前)とホスト情報(@の後)を分離
            int atIndex = cleanUrl.LastIndexOf('@');
            string userPart = cleanUrl.Substring(0, atIndex);
            string hostPart = cleanUrl.Substring(atIndex + 1);

            // ユーザー名とパスワードを分離
            string[] userSplit = userPart.Split(':');
            string user = userSplit[0];
            string pass = userSplit.Length > 1 ? userSplit[1] : "";

            // ホスト、ポート、データベース名を分離
            string[] hostSplit = hostPart.Split('/');
            string[] hostAndPort = hostSplit[0].Split(':');
            string host = hostAndPort[0];
            string port = hostAndPort.Length > 1 ? hostAndPort[1] : "5432";
            string db = hostSplit.Length > 1 ? hostSplit[1] : "railway";

            // 3. Railwayで必須の SSL 設定を強制的に付与して返却
            return $"Host={host};Port={port};Database={db};Username={user};Password={pass};SSL Mode=Require;Trust Server Certificate=true;Pooling=true;";
        }
        catch (Exception ex)
        {
            // 解析に失敗した場合の予備策
            Console.WriteLine($"DB URLの解析に失敗しました: {ex.Message}");
            return url.Contains("?") ? $"{url}&sslmode=Require" : $"{url}?sslmode=Require";
        }
    }
}

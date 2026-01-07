using Npgsql;

namespace DiscordTimeSignal.Data;

public static class DbConfig
{
    public static string GetConnectionString()
    {
        // 公開URLを優先的に使用
        var url = Environment.GetEnvironmentVariable("DATABASE_PUBLIC_URL") 
                  ?? Environment.GetEnvironmentVariable("DATABASE_URL");

        if (string.IsNullOrEmpty(url)) throw new Exception("Database URL is missing.");

        // postgresql:// を解析可能な形式に変換
        var normalizedUrl = url.Replace("postgresql://", "postgres://");
        
        if (normalizedUrl.StartsWith("postgres://"))
        {
            var uri = new Uri(normalizedUrl);
            var userInfo = uri.UserInfo.Split(':');
            var user = userInfo[0];
            var password = userInfo.Length > 1 ? userInfo[1] : "";

            // SSL設定と証明書信頼設定を付与
            return $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.Trim('/')};Username={user};Password={password};SSL Mode=Require;Trust Server Certificate=true;Pooling=true;";
        }
        return url;
    }
}

using Npgsql;

public static class DatabaseConfig
{
    public static string GetConnectionString()
    {
        // Railwayの環境変数 DATABASE_URL (postgres://user:pass@host:port/db) を取得
        var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");

        if (string.IsNullOrEmpty(databaseUrl))
        {
            // ローカル開発用
            return "Host=localhost;Username=postgres;Password=password;Database=discord_bot";
        }

        var databaseUri = new Uri(databaseUrl);
        var userInfo = databaseUri.UserInfo.Split(':');

        return new NpgsqlConnectionStringBuilder
        {
            Host = databaseUri.Host,
            Port = databaseUri.Port,
            Username = userInfo[0],
            Password = userInfo[1],
            Database = databaseUri.LocalPath.TrimStart('/'),
            SslMode = SslMode.Require,
            TrustServerCertificate = true // Railwayではこれが必要な場合が多い
        }.ToString();
    }
}

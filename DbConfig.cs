using System;

public static class DbConfig
{
    public static string GetConnectionString()
    {
        // 余計なことは一切せず、環境変数をそのまま返す
        // 解析は環境変数側（Railway側）で既に行われている前提にする
        var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL");
        
        if (string.IsNullOrEmpty(connectionString))
        {
            Console.WriteLine("[DbConfig] Error: DATABASE_URL is null or empty.");
            return "";
        }

        return connectionString;
    }
}

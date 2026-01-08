using System;

public static class DbConfig
{
    public static string GetConnectionString()
    {
        // 環境変数を取得するだけ。余計な解析（Builder）は一切しない。
        var url = Environment.GetEnvironmentVariable("DATABASE_URL");
        
        if (string.IsNullOrEmpty(url))
        {
            Console.WriteLine("[DbConfig] DATABASE_URL is empty.");
            return "";
        }

        return url;
    }
}

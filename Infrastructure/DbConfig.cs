using Npgsql;
using System;

namespace Discord_bot.Infrastructure
{
    public class DbConfig
    {
        public string GetConnectionString()
        {
            var rawUrl = Environment.GetEnvironmentVariable("DATABASE_URL");

            if (string.IsNullOrEmpty(rawUrl))
            {
                Console.WriteLine("[DB Error] DATABASE_URL is EMPTY.");
                return null;
            }

            // 前後の空白や引用符を徹底的に除去
            var databaseUrl = rawUrl.Trim().Trim('"').Trim('\'');

            try
            {
                // Uriクラスを使わずに直接パースを試みる（より安全な方法）
                if (databaseUrl.StartsWith("postgres://"))
                {
                    var uri = new Uri(databaseUrl);
                    var userInfo = uri.UserInfo.Split(':');

                    return new NpgsqlConnectionStringBuilder
                    {
                        Host = uri.Host,
                        Port = uri.Port,
                        Username = userInfo[0],
                        Password = userInfo[1],
                        Database = uri.LocalPath.TrimStart('/'),
                        SslMode = SslMode.Require,
                        TrustServerCertificate = true
                    }.ToString();
                }
                else
                {
                    // postgres:// 形式でない場合、そのまま接続文字列として扱ってみる
                    Console.WriteLine("[DB Warning] URL does not start with postgres://. Using as raw connection string.");
                    return databaseUrl;
                }
            }
            catch (Exception ex)
            {
                // エラー時、URLの最初の20文字だけ表示して中身を確認（セキュリティのため全表示は避ける）
                string hint = databaseUrl.Length > 20 ? databaseUrl.Substring(0, 20) : databaseUrl;
                Console.WriteLine($"[DB Error] Parsing Failed at: {hint}...");
                Console.WriteLine($"[DB Error] Details: {ex.Message}");
                return null;
            }
        }

        public NpgsqlConnection GetConnection() 
        {
            var connectionString = GetConnectionString();
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException("DATABASE_URL config is invalid.");
            }
            return new NpgsqlConnection(connectionString);
        }
    }
}

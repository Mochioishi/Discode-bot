using Npgsql;
using System;

namespace DiscordBot.Infrastructure
{
    public static class DbConfig
    {
        public static string GetConnectionString()
        {
            var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");

            if (string.IsNullOrEmpty(databaseUrl))
            {
                return "Host=localhost;Username=postgres;Password=password;Database=discord_bot";
            }

            try
            {
                var builder = new NpgsqlConnectionStringBuilder(databaseUrl);
                builder.SslMode = SslMode.Disable;
                builder.TrustServerCertificate = true;
                return builder.ToString();
            }
            catch
            {
                return databaseUrl;
            }
        }
    }
}

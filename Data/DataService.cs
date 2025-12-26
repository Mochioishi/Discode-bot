using Npgsql;

namespace DiscordTimeSignal.Data;

public class DataService
{
    private readonly string _connectionString;

    public DataService(IConfiguration config)
    {
        // Railway PostgreSQL 環境変数
        var host = Environment.GetEnvironmentVariable("PGHOST");
        var port = Environment.GetEnvironmentVariable("PGPORT");
        var user = Environment.GetEnvironmentVariable("PGUSER");
        var pass = Environment.GetEnvironmentVariable("PGPASSWORD");
        var db   = Environment.GetEnvironmentVariable("PGDATABASE");

        if (!string.IsNullOrWhiteSpace(host))
        {
            // Railway の接続文字列
            _connectionString =
                $"Host={host};Port={port};Username={user};Password={pass};Database={db};SSL Mode=Require;Trust Server Certificate=true";
        }
        else
        {
            // ローカル or appsettings.json
            _connectionString = config.GetConnectionString("Postgres")
                ?? throw new Exception("PostgreSQL 接続文字列が設定されていません。");
        }
    }

    // ============================
    // ここから下は GitHub のコードをそのまま
    // ============================

    public async Task EnsureTablesAsync()
    {
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        var sql = @"
CREATE TABLE IF NOT EXISTS deleteago (
    id SERIAL PRIMARY KEY,
    guild_id BIGINT NOT NULL,
    channel_id BIGINT NOT NULL,
    days INT NOT NULL,
    protect_mode TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS rolegive (
    id SERIAL PRIMARY KEY,
    guild_id BIGINT NOT NULL,
    channel_id BIGINT NOT NULL,
    message_id BIGINT NOT NULL,
    role_id BIGINT NOT NULL,
    emoji TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS prsk_roomid (
    id SERIAL PRIMARY KEY,
    guild_id BIGINT NOT NULL,
    watch_channel_id BIGINT NOT NULL,
    target_channel_id BIGINT NOT NULL,
    name_format TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS bottext (
    id SERIAL PRIMARY KEY,
    guild_id BIGINT NOT NULL,
    channel_id BIGINT NOT NULL,
    content TEXT NOT NULL,
    is_embed BOOLEAN NOT NULL,
    embed_title TEXT,
    time_hhmm TEXT NOT NULL
);
";

        using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    // 以降の CRUD メソッドは GitHub のまま
    // （長いので省略しているが、あなたの GitHub の内容をそのまま使えばOK）
}

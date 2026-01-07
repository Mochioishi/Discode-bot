using System.Data;
using Npgsql;
using Microsoft.Extensions.Configuration;

namespace DiscordTimeSignal.Data;

public class DataService
{
    private readonly string _connectionString;
    public Func<NpgsqlConnection> ConnectionFactory { get; set; }

    public DataService(IConfiguration config)
    {
        // ここで DbConfig を呼んでいないと、古い(間違った)接続情報が使われ続けます
        _connectionString = DbConfig.GetConnectionString();
        ConnectionFactory = () => new NpgsqlConnection(_connectionString);
    }

    private NpgsqlConnection GetConnection() => ConnectionFactory();

    public async Task EnsureTablesAsync()
    {
        using var conn = GetConnection();
        await conn.OpenAsync(); // ここが 28 行目付近

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
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
                embed_title TEXT NULL,
                time_hhmm TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS deleteago (
                id SERIAL PRIMARY KEY,
                guild_id BIGINT NOT NULL,
                channel_id BIGINT NOT NULL,
                days INT NOT NULL,
                protect_mode TEXT NOT NULL
            );";
        await cmd.ExecuteNonQueryAsync();
    }

    // ... 以下、GetRoleGivesByGuildAsync や UpdateDeleteAgoAsync などの既存メソッドを全て含めてください ...
}

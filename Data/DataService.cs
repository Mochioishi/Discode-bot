using Npgsql;
using DiscordTimeSignal.Data.Models;

namespace DiscordTimeSignal.Data;

public class DataService
{
    private readonly string _connectionString;

    public DataService(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("Postgres")
            ?? Environment.GetEnvironmentVariable("DATABASE_URL")
            ?? throw new Exception("PostgreSQL 接続文字列が設定されていません。");
    }

    // ============================================================
    // テーブル自動生成
    // ============================================================
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

    // ============================================================
    // deleteago
    // ============================================================
    public async Task<IEnumerable<DeleteAgoEntry>> GetAllDeleteAgoAsync()
    {
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        var cmd = new NpgsqlCommand(
            "SELECT id, guild_id, channel_id, days, protect_mode FROM deleteago ORDER BY id ASC",
            conn);

        var list = new List<DeleteAgoEntry>();
        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            list.Add(new DeleteAgoEntry
            {
                Id = reader.GetInt32(0),
                GuildId = (ulong)reader.GetInt64(1),
                ChannelId = (ulong)reader.GetInt64(2),
                Days = reader.GetInt32(3),
                ProtectMode = reader.GetString(4)
            });
        }

        return list;
    }

    public async Task AddDeleteAgoAsync(DeleteAgoEntry entry)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        var cmd = new NpgsqlCommand(
            @"INSERT INTO deleteago (guild_id, channel_id, days, protect_mode)
              VALUES (@g, @c, @d, @p)",
            conn);

        cmd.Parameters.AddWithValue("g", (long)entry.GuildId);
        cmd.Parameters.AddWithValue("c", (long)entry.ChannelId);
        cmd.Parameters.AddWithValue("d", entry.Days);
        cmd.Parameters.AddWithValue("p", entry.ProtectMode);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpdateDeleteAgoAsync(DeleteAgoEntry entry)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        var cmd = new NpgsqlCommand(
            @"UPDATE deleteago
              SET days = @d, protect_mode = @p
              WHERE id = @id",
            conn);

        cmd.Parameters.AddWithValue("id", entry.Id);
        cmd.Parameters.AddWithValue("d", entry.Days);
        cmd.Parameters.AddWithValue("p", entry.ProtectMode);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteDeleteAgoAsync(long id)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        var cmd = new NpgsqlCommand("DELETE FROM deleteago WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", id);

        await cmd.ExecuteNonQueryAsync();
    }

    // ============================================================
    // rolegive
    // ============================================================
    public async Task<IEnumerable<RoleGiveEntry>> GetRoleGivesByGuildAsync(ulong guildId)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        var cmd = new NpgsqlCommand(
            @"SELECT id, guild_id, channel_id, message_id, role_id, emoji
              FROM rolegive
              WHERE guild_id = @g
              ORDER BY id ASC",
            conn);

        cmd.Parameters.AddWithValue("g", (long)guildId);

        var list = new List<RoleGiveEntry>();
        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            list.Add(new RoleGiveEntry
            {
                Id = reader.GetInt32(0),
                GuildId = (ulong)reader.GetInt64(1),
                ChannelId = (ulong)reader.GetInt64(2),
                MessageId = (ulong)reader.GetInt64(3),
                RoleId = (ulong)reader.GetInt64(4),
                Emoji = reader.GetString(5)
            });
        }

        return list;
    }

    public async Task<RoleGiveEntry?> GetRoleGiveByMessageAsync(ulong guildId, ulong channelId, ulong messageId)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        var cmd = new NpgsqlCommand(
            @"SELECT id, guild_id, channel_id, message_id, role_id, emoji
              FROM rolegive
              WHERE guild_id = @g AND channel_id = @c AND message_id = @m",
            conn);

        cmd.Parameters.AddWithValue("g", (long)guildId);
        cmd.Parameters.AddWithValue("c", (long)channelId);
        cmd.Parameters.AddWithValue("m", (long)messageId);

        using var reader = await cmd.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
            return null;

        return new RoleGiveEntry
        {
            Id = reader.GetInt32(0),
            GuildId = (ulong)reader.GetInt64(1),
            ChannelId = (ulong)reader.GetInt64(2),
            MessageId = (ulong)reader.GetInt64(3),
            RoleId = (ulong)reader.GetInt64(4),
            Emoji = reader.GetString(5)
        };
    }

    public async Task AddRoleGiveAsync(RoleGiveEntry entry)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        var cmd = new NpgsqlCommand(
            @"INSERT INTO rolegive (guild_id, channel_id, message_id, role_id, emoji)
              VALUES (@g, @c, @m, @r, @e)",
            conn);

        cmd.Parameters.AddWithValue("g", (long)entry.GuildId);
        cmd.Parameters.AddWithValue("c", (long)entry.ChannelId);
        cmd.Parameters.AddWithValue("m", (long)entry.MessageId);
        cmd.Parameters.AddWithValue("r", (long)entry.RoleId);
        cmd.Parameters.AddWithValue("e", entry.Emoji);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteRoleGiveAsync(long id)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        var cmd = new NpgsqlCommand("DELETE FROM rolegive WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", id);

        await cmd.ExecuteNonQueryAsync();
    }

    // ============================================================
    // prsk_roomid
    // ============================================================
    public async Task<IEnumerable<PrskRoomIdEntry>> GetPrskRoomIdsAsync(ulong guildId)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        var cmd = new NpgsqlCommand(
            @"SELECT id, guild_id, watch_channel_id, target_channel_id, name_format
              FROM prsk_roomid
              WHERE guild_id = @g
              ORDER BY id ASC",
            conn);

        cmd.Parameters.AddWithValue("g", (long)guildId);

        var list = new List<PrskRoomIdEntry>();
        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            list.Add(new PrskRoomIdEntry
            {
                Id = reader.GetInt32(0),
                GuildId = (ulong)reader.GetInt64(1),
                WatchChannelId = (ulong)reader.GetInt64(2),
                TargetChannelId = (ulong)reader.GetInt64(3),
                NameFormat = reader.GetString(4)
            });
        }

        return list;
    }

    public async Task AddPrskRoomIdAsync(PrskRoomIdEntry entry)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        var cmd = new NpgsqlCommand(
            @"INSERT INTO prsk_roomid (guild_id, watch_channel_id, target_channel_id, name_format)
              VALUES (@g, @w, @t, @f)",
            conn);

        cmd.Parameters.AddWithValue("g", (long)entry.GuildId);
        cmd.Parameters.AddWithValue("w", (long)entry.WatchChannelId);
        cmd.Parameters.AddWithValue("t", (long)entry.TargetChannelId);
        cmd.Parameters.AddWithValue("f", entry.NameFormat);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeletePrskRoomIdAsync(long id)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        var cmd = new NpgsqlCommand("DELETE FROM prsk_roomid WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", id);

        await cmd.ExecuteNonQueryAsync();
    }

    // ============================================================
    // bottext
    // ============================================================
    public async Task<IEnumerable<BotTextEntry>> GetBotTextsAsync(ulong guildId, ulong channelId)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        var cmd = new NpgsqlCommand(
            @"SELECT id, guild_id, channel_id, content, is_embed, embed_title, time_hhmm
              FROM bottext
              WHERE guild_id = @g AND channel_id = @c
              ORDER BY id ASC",
            conn);

        cmd.Parameters.AddWithValue("g", (long)guildId);
        cmd.Parameters.AddWithValue("c", (long)channelId);

        var list = new List<BotTextEntry>();
        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            list.Add(new BotTextEntry
            {
                Id = reader.GetInt64(0),
                GuildId = (ulong)reader.GetInt64(1),
                ChannelId = (ulong)reader.GetInt64(2),
                Content = reader.GetString(3),
                IsEmbed = reader.GetBoolean(4),
                EmbedTitle = reader.IsDBNull(5) ? null : reader.GetString(5),
                TimeHhmm = reader.GetString(6)
            });
        }

        return list;
    }

    // ★ ギルド全体の bottext を取得（新規追加）
    public async Task<IEnumerable<BotTextEntry>> GetBotTextsByGuildAsync(ulong guildId)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        var cmd = new NpgsqlCommand(
            @"SELECT id, guild_id, channel_id, content, is_embed, embed_title, time_hhmm
              FROM bottext
              WHERE guild_id = @g
              ORDER BY id ASC",
            conn);

        cmd.Parameters.AddWithValue("g", (long)guildId);

        var list = new List<BotTextEntry>();
        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            list.Add(new BotTextEntry
            {
                Id = reader.GetInt64(0),
                GuildId = (ulong)reader.GetInt64(1),
                ChannelId = (ulong)reader.GetInt64(2),
                Content = reader.GetString(3),
                IsEmbed = reader.GetBoolean(4),
                EmbedTitle = reader.IsDBNull(5) ? null : reader.GetString(5),
                TimeHhmm = reader.GetString(6)
            });
        }

        return list;
    }

    public async Task<long> AddBotTextAsync(BotTextEntry entry)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        var cmd = new NpgsqlCommand(
            @"INSERT INTO bottext (guild_id, channel_id, content, is_embed, embed_title, time_hhmm)
              VALUES (@g, @c, @t, @e, @title, @time)
              RETURNING id",
            conn);

        cmd.Parameters.AddWithValue("g", (long)entry.GuildId);
        cmd.Parameters.AddWithValue("c", (long)entry.ChannelId);
        cmd.Parameters.AddWithValue("t", entry.Content);
        cmd.Parameters.AddWithValue("e", entry.IsEmbed);
        cmd.Parameters.AddWithValue("title", (object?)entry.EmbedTitle ?? DBNull.Value);
        cmd.Parameters.AddWithValue("time", entry.TimeHhmm);

        return (long)(await cmd.ExecuteScalarAsync()!);
    }

    public async Task DeleteBotTextAsync(long id)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        var cmd = new NpgsqlCommand("DELETE FROM bottext WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", id);

        await cmd.ExecuteNonQueryAsync();
    }
}

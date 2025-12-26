using System.Data;
using Dapper;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace DiscordTimeSignal.Data;

public class DataService
{
    private readonly string _connectionString;

    public DataService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("Default") 
            ?? throw new InvalidOperationException("Connection string 'Default' not found.");
    }

    private IDbConnection CreateConnection()
        => new NpgsqlConnection(_connectionString);

    // ============================================
    //  テーブル自動生成
    // ============================================
    public async Task EnsureTablesAsync()
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        var sql = @"
CREATE TABLE IF NOT EXISTS bottext (
    id SERIAL PRIMARY KEY,
    guild_id BIGINT NOT NULL,
    channel_id BIGINT NOT NULL,
    content TEXT NOT NULL,
    is_embed BOOLEAN NOT NULL DEFAULT FALSE,
    embed_title TEXT,
    time_hhmm TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS deleteago (
    id SERIAL PRIMARY KEY,
    guild_id BIGINT NOT NULL,
    channel_id BIGINT NOT NULL,
    days INT NOT NULL,
    protect_mode TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS prsk_roomid (
    id SERIAL PRIMARY KEY,
    guild_id BIGINT NOT NULL,
    watch_channel_id BIGINT NOT NULL,
    target_channel_id BIGINT NOT NULL,
    name_format TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS rolegive (
    id SERIAL PRIMARY KEY,
    guild_id BIGINT NOT NULL,
    channel_id BIGINT NOT NULL,
    message_id BIGINT NOT NULL,
    role_id BIGINT NOT NULL,
    emoji TEXT NOT NULL
);
";

        using var cmd = new NpgsqlCommand(sql, (NpgsqlConnection)conn);
        await cmd.ExecuteNonQueryAsync();
    }

    // ---------- bottext ----------
    public async Task<IEnumerable<BotTextEntry>> GetBotTextsAsync(ulong guildId, ulong channelId)
    {
        const string sql = """
        SELECT id, guild_id, channel_id, content, is_embed, embed_title, time_hhmm
        FROM bottext
        WHERE guild_id = @GuildId AND channel_id = @ChannelId
        ORDER BY id;
        """;

        using var conn = CreateConnection();
        return await conn.QueryAsync<BotTextEntry>(sql, new { GuildId = (long)guildId, ChannelId = (long)channelId });
    }

    public async Task<long> AddBotTextAsync(BotTextEntry entry)
    {
        const string sql = """
        INSERT INTO bottext (guild_id, channel_id, content, is_embed, embed_title, time_hhmm)
        VALUES (@GuildId, @ChannelId, @Content, @IsEmbed, @EmbedTitle, @TimeHhmm)
        RETURNING id;
        """;

        using var conn = CreateConnection();
        return await conn.ExecuteScalarAsync<long>(sql, new
        {
            GuildId = (long)entry.GuildId,
            ChannelId = (long)entry.ChannelId,
            entry.Content,
            entry.IsEmbed,
            entry.EmbedTitle,
            entry.TimeHhmm
        });
    }

    public async Task DeleteBotTextAsync(long id)
    {
        const string sql = "DELETE FROM bottext WHERE id = @Id;";
        using var conn = CreateConnection();
        await conn.ExecuteAsync(sql, new { Id = id });
    }

    // ---------- deleteago ----------
    public async Task<IEnumerable<DeleteAgoEntry>> GetDeleteAgoAsync(ulong guildId, ulong channelId)
    {
        const string sql = """
        SELECT id, guild_id, channel_id, days, protect_mode
        FROM deleteago
        WHERE guild_id = @GuildId AND channel_id = @ChannelId
        ORDER BY id;
        """;

        using var conn = CreateConnection();
        return await conn.QueryAsync<DeleteAgoEntry>(sql, new { GuildId = (long)guildId, ChannelId = (long)channelId });
    }

    public async Task<long> AddDeleteAgoAsync(DeleteAgoEntry entry)
    {
        const string sql = """
        INSERT INTO deleteago (guild_id, channel_id, days, protect_mode)
        VALUES (@GuildId, @ChannelId, @Days, @ProtectMode)
        RETURNING id;
        """;

        using var conn = CreateConnection();
        return await conn.ExecuteScalarAsync<long>(sql, new
        {
            GuildId = (long)entry.GuildId,
            ChannelId = (long)entry.ChannelId,
            entry.Days,
            entry.ProtectMode
        });
    }

    public async Task UpdateDeleteAgoAsync(DeleteAgoEntry entry)
    {
        const string sql = """
        UPDATE deleteago
        SET days = @Days, protect_mode = @ProtectMode
        WHERE id = @Id;
        """;

        using var conn = CreateConnection();
        await conn.ExecuteAsync(sql, new { entry.Id, entry.Days, entry.ProtectMode });
    }

    public async Task DeleteDeleteAgoAsync(long id)
    {
        const string sql = "DELETE FROM deleteago WHERE id = @Id;";
        using var conn = CreateConnection();
        await conn.ExecuteAsync(sql, new { Id = id });
    }

    public async Task<IEnumerable<DeleteAgoEntry>> GetAllDeleteAgoAsync()
    {
        const string sql = """
        SELECT id, guild_id, channel_id, days, protect_mode
        FROM deleteago;
        """;

        using var conn = CreateConnection();
        return await conn.QueryAsync<DeleteAgoEntry>(sql);
    }

    // ---------- prsk_roomid ----------
    public async Task<IEnumerable<PrskRoomIdEntry>> GetPrskRoomIdsAsync(ulong guildId)
    {
        const string sql = """
        SELECT id, guild_id, watch_channel_id, target_channel_id, name_format
        FROM prsk_roomid
        WHERE guild_id = @GuildId
        ORDER BY id;
        """;

        using var conn = CreateConnection();
        return await conn.QueryAsync<PrskRoomIdEntry>(sql, new { GuildId = (long)guildId });
    }

    public async Task<long> AddPrskRoomIdAsync(PrskRoomIdEntry entry)
    {
        const string sql = """
        INSERT INTO prsk_roomid (guild_id, watch_channel_id, target_channel_id, name_format)
        VALUES (@GuildId, @WatchChannelId, @TargetChannelId, @NameFormat)
        RETURNING id;
        """;

        using var conn = CreateConnection();
        return await conn.ExecuteScalarAsync<long>(sql, new
        {
            GuildId = (long)entry.GuildId,
            WatchChannelId = (long)entry.WatchChannelId,
            TargetChannelId = (long)entry.TargetChannelId,
            entry.NameFormat
        });
    }

    public async Task DeletePrskRoomIdAsync(long id)
    {
        const string sql = "DELETE FROM prsk_roomid WHERE id = @Id;";
        using var conn = CreateConnection();
        await conn.ExecuteAsync(sql, new { Id = id });
    }

    // ---------- rolegive ----------
    public async Task<IEnumerable<RoleGiveEntry>> GetRoleGivesAsync(ulong guildId, ulong channelId)
    {
        const string sql = """
        SELECT id, guild_id, channel_id, message_id, role_id, emoji
        FROM rolegive
        WHERE guild_id = @GuildId AND channel_id = @ChannelId
        ORDER BY id;
        """;

        using var conn = CreateConnection();
        return await conn.QueryAsync<RoleGiveEntry>(sql, new { GuildId = (long)guildId, ChannelId = (long)channelId });
    }

    public async Task<RoleGiveEntry?> GetRoleGiveByMessageAsync(ulong guildId, ulong channelId, ulong messageId)
    {
        const string sql = """
        SELECT id, guild_id, channel_id, message_id, role_id, emoji
        FROM rolegive
        WHERE guild_id = @GuildId AND channel_id = @ChannelId AND message_id = @MessageId;
        """;

        using var conn = CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<RoleGiveEntry>(sql, new
        {
            GuildId = (long)guildId,
            ChannelId = (long)channelId,
            MessageId = (long)messageId
        });
    }

    public async Task<long> AddRoleGiveAsync(RoleGiveEntry entry)
    {
        const string sql = """
        INSERT INTO rolegive (guild_id, channel_id, message_id, role_id, emoji)
        VALUES (@GuildId, @ChannelId, @MessageId, @RoleId, @Emoji)
        RETURNING id;
        """;

        using var conn = CreateConnection();
        return await conn.ExecuteScalarAsync<long>(sql, new
        {
            GuildId = (long)entry.GuildId,
            ChannelId = (long)entry.ChannelId,
            MessageId = (long)entry.MessageId,
            RoleId = (long)entry.RoleId,
            entry.Emoji
        });
    }

    public async Task DeleteRoleGiveAsync(long id)
    {
        const string sql = "DELETE FROM rolegive WHERE id = @Id;";
        using var conn = CreateConnection();
        await conn.ExecuteAsync(sql, new { Id = id });
    }

    public async Task<IEnumerable<RoleGiveEntry>> GetRoleGivesByGuildAsync(ulong guildId)
    {
        const string sql = """
        SELECT id, guild_id, channel_id, message_id, role_id, emoji
        FROM rolegive
        WHERE guild_id = @GuildId;
        """;

        using var conn = CreateConnection();
        return await conn.QueryAsync<RoleGiveEntry>(sql, new { GuildId = (long)guildId });
    }
}

// DTO / レコード（省略：あなたの元コードのままでOK）

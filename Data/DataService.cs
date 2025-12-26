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

    // ---------- Delete_Range（メモリで十分ならDB不要でもOKだが、ここではDB版雛形だけ） ----------
    // 必要ならここに range 用テーブルのCRUDを書く
}

// DTO / レコード

public record BotTextEntry
{
    public long Id { get; init; }
    public ulong GuildId { get; init; }
    public ulong ChannelId { get; init; }
    public string Content { get; init; } = "";
    public bool IsEmbed { get; init; }
    public string? EmbedTitle { get; init; }
    public string TimeHhmm { get; init; } = "00:00";
}

public record DeleteAgoEntry
{
    public long Id { get; init; }
    public ulong GuildId { get; init; }
    public ulong ChannelId { get; init; }
    public int Days { get; init; }
    public string ProtectMode { get; init; } = "none"; // none,image,reaction,both
}

public record PrskRoomIdEntry
{
    public long Id { get; init; }
    public ulong GuildId { get; init; }
    public ulong WatchChannelId { get; init; }
    public ulong TargetChannelId { get; init; }
    public string NameFormat { get; init; } = "ex【{roomid}】";
}

public record RoleGiveEntry
{
    public long Id { get; init; }
    public ulong GuildId { get; init; }
    public ulong ChannelId { get; init; }
    public ulong MessageId { get; init; }
    public ulong RoleId { get; init; }
    public string Emoji { get; init; } = "🐾";
}

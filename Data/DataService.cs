using System.Data;
using Npgsql;
using Microsoft.Extensions.Configuration;

namespace DiscordTimeSignal.Data;

// =======================
// DataService 本体
// =======================
public class DataService
{
    private readonly string _connectionString;

    public DataService(IConfiguration config)
    {
        // 1. Railwayの環境変数を取得
        var url = Environment.GetEnvironmentVariable("DATABASE_URL");

        if (string.IsNullOrEmpty(url))
        {
            throw new Exception("環境変数 DATABASE_URL が設定されていません。RailwayのVariablesを確認してください。");
        }

        // 2. postgres:// 形式を Npgsql が確実に解釈できる形式に変換
        // 接続エラーを防ぐため、SSL設定と証明書信頼設定を強制付与します
        _connectionString = ConvertPostgresUrlToConnectionString(url);
    }

    /// <summary>
    /// postgres:// 形式の URL を Npgsql 用の接続文字列に変換する
    /// </summary>
    private string ConvertPostgresUrlToConnectionString(string url)
    {
        if (!url.Contains("://")) return url;

        try 
        {
            var uri = new Uri(url);
            var userInfo = uri.UserInfo.Split(':');
            var user = userInfo[0];
            var password = userInfo.Length > 1 ? userInfo[1] : "";

            // RailwayのPostgres接続には SSL Mode=Require と Trust Server Certificate=true が必須です
            return $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.Trim('/')};Username={user};Password={password};SSL Mode=Require;Trust Server Certificate=true;";
        }
        catch
        {
            // 解析に失敗した場合は、SSL設定だけ付け足してそのまま返す
            return url.Contains("?") ? $"{url}&sslmode=Require" : $"{url}?sslmode=Require";
        }
    }

    private NpgsqlConnection GetConnection()
        => new NpgsqlConnection(_connectionString);

    // ============================================================
    // テーブル初期化
    // ============================================================
    public async Task EnsureTablesAsync()
    {
        using var conn = GetConnection();
        await conn.OpenAsync();

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
            );
        ";

        await cmd.ExecuteNonQueryAsync();
    }

    // ============================================================
    // RoleGive
    // ============================================================
    public async Task AddRoleGiveAsync(RoleGiveEntry entry)
    {
        entry.Emoji = NormalizeEmoji(entry.Emoji);

        using var conn = GetConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO rolegive (guild_id, channel_id, message_id, role_id, emoji)
            VALUES (@g, @c, @m, @r, @e);
        ";

        cmd.Parameters.AddWithValue("@g", (long)entry.GuildId);
        cmd.Parameters.AddWithValue("@c", (long)entry.ChannelId);
        cmd.Parameters.AddWithValue("@m", (long)entry.MessageId);
        cmd.Parameters.AddWithValue("@r", (long)entry.RoleId);
        cmd.Parameters.AddWithValue("@e", entry.Emoji);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<IEnumerable<RoleGiveEntry>> GetRoleGivesByGuildAsync(ulong guildId)
    {
        var list = new List<RoleGiveEntry>();

        using var conn = GetConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, guild_id, channel_id, message_id, role_id, emoji
            FROM rolegive
            WHERE guild_id = @g;
        ";
        cmd.Parameters.AddWithValue("@g", (long)guildId);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new RoleGiveEntry
            {
                Id = reader.GetInt64(0),
                GuildId = (ulong)reader.GetInt64(1),
                ChannelId = (ulong)reader.GetInt64(2),
                MessageId = (ulong)reader.GetInt64(3),
                RoleId = (ulong)reader.GetInt64(4),
                Emoji = NormalizeEmoji(reader.GetString(5))
            });
        }

        return list;
    }

    public async Task<RoleGiveEntry?> GetRoleGiveByMessageAsync(ulong guildId, ulong channelId, ulong messageId)
    {
        using var conn = GetConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, guild_id, channel_id, message_id, role_id, emoji
            FROM rolegive
            WHERE guild_id = @g AND channel_id = @c AND message_id = @m;
        ";

        cmd.Parameters.AddWithValue("@g", (long)guildId);
        cmd.Parameters.AddWithValue("@c", (long)channelId);
        cmd.Parameters.AddWithValue("@m", (long)messageId);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new RoleGiveEntry
            {
                Id = reader.GetInt64(0),
                GuildId = (ulong)reader.GetInt64(1),
                ChannelId = (ulong)reader.GetInt64(2),
                MessageId = (ulong)reader.GetInt64(3),
                RoleId = (ulong)reader.GetInt64(4),
                Emoji = NormalizeEmoji(reader.GetString(5))
            };
        }

        return null;
    }

    public async Task DeleteRoleGiveAsync(long id)
    {
        using var conn = GetConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"DELETE FROM rolegive WHERE id = @id;";
        cmd.Parameters.AddWithValue("@id", id);

        await cmd.ExecuteNonQueryAsync();
    }

    private string NormalizeEmoji(string emoji)
    {
        if (!emoji.Contains(':'))
            return emoji;

        if (emoji.StartsWith("<:") && emoji.EndsWith(">"))
            return emoji;

        return $"<{emoji}>";
    }

    // ============================================================
    // PrskRoomId
    // ============================================================
    public async Task AddPrskRoomIdAsync(PrskRoomIdEntry entry)
    {
        using var conn = GetConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO prsk_roomid (guild_id, watch_channel_id, target_channel_id, name_format)
            VALUES (@g, @w, @t, @n);
        ";

        cmd.Parameters.AddWithValue("@g", (long)entry.GuildId);
        cmd.Parameters.AddWithValue("@w", (long)entry.WatchChannelId);
        cmd.Parameters.AddWithValue("@t", (long)entry.TargetChannelId);
        cmd.Parameters.AddWithValue("@n", entry.NameFormat ?? "");

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<IEnumerable<PrskRoomIdEntry>> GetPrskRoomIdsAsync(ulong guildId)
    {
        var list = new List<PrskRoomIdEntry>();

        using var conn = GetConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, guild_id, watch_channel_id, target_channel_id, name_format
            FROM prsk_roomid
            WHERE guild_id = @g;
        ";
        cmd.Parameters.AddWithValue("@g", (long)guildId);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new PrskRoomIdEntry
            {
                Id = reader.GetInt64(0),
                GuildId = (ulong)reader.GetInt64(1),
                WatchChannelId = (ulong)reader.GetInt64(2),
                TargetChannelId = (ulong)reader.GetInt64(3),
                NameFormat = reader.IsDBNull(4) ? "" : reader.GetString(4)
            });
        }

        return list;
    }

    public async Task DeletePrskRoomIdAsync(long id)
    {
        using var conn = GetConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"DELETE FROM prsk_roomid WHERE id = @id;";
        cmd.Parameters.AddWithValue("@id", id);

        await cmd.ExecuteNonQueryAsync();
    }

    // ============================================================
    // BotText
    // ============================================================
    public async Task<long> AddBotTextAsync(BotTextEntry entry)
    {
        using var conn = GetConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO bottext (guild_id, channel_id, content, is_embed, embed_title, time_hhmm)
            VALUES (@g, @c, @content, @embed, @title, @time)
            RETURNING id;
        ";

        cmd.Parameters.AddWithValue("@g", (long)entry.GuildId);
        cmd.Parameters.AddWithValue("@c", (long)entry.ChannelId);
        cmd.Parameters.AddWithValue("@content", entry.Content);
        cmd.Parameters.AddWithValue("@embed", entry.IsEmbed);
        cmd.Parameters.AddWithValue("@title", (object?)entry.EmbedTitle ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@time", entry.TimeHhmm);

        var result = await cmd.ExecuteScalarAsync();
        return (long)result!;
    }

    public async Task<IEnumerable<BotTextEntry>> GetBotTextsByGuildAsync(ulong guildId)
    {
        var list = new List<BotTextEntry>();

        using var conn = GetConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, guild_id, channel_id, content, is_embed, embed_title, time_hhmm
            FROM bottext
            WHERE guild_id = @g;
        ";
        cmd.Parameters.AddWithValue("@g", (long)guildId);

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

    public async Task DeleteBotTextAsync(long id)
    {
        using var conn = GetConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"DELETE FROM bottext WHERE id = @id;";
        cmd.Parameters.AddWithValue("@id", id);

        await cmd.ExecuteNonQueryAsync();
    }

    // ============================================================
    // DeleteAgo
    // ============================================================
    public async Task AddDeleteAgoAsync(DeleteAgoEntry entry)
    {
        using var conn = GetConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO deleteago (guild_id, channel_id, days, protect_mode)
            VALUES (@g, @c, @d, @p);
        ";

        cmd.Parameters.AddWithValue("@g", (long)entry.GuildId);
        cmd.Parameters.AddWithValue("@c", (long)entry.ChannelId);
        cmd.Parameters.AddWithValue("@d", entry.Days);
        cmd.Parameters.AddWithValue("@p", entry.ProtectMode ?? "none");

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<IEnumerable<DeleteAgoEntry>> GetAllDeleteAgoAsync()
    {
        var list = new List<DeleteAgoEntry>();

        using var conn = GetConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, guild_id, channel_id, days, protect_mode
            FROM deleteago;
        ";

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new DeleteAgoEntry
            {
                Id = reader.GetInt64(0),
                GuildId = (ulong)reader.GetInt64(1),
                ChannelId = (ulong)reader.GetInt64(2),
                Days = reader.GetInt32(3),
                ProtectMode = reader.IsDBNull(4) ? "none" : reader.GetString(4)
            });
        }

        return list;
    }

    public async Task DeleteDeleteAgoAsync(long id)
    {
        using var conn = GetConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"DELETE FROM deleteago WHERE id = @id;";
        cmd.Parameters.AddWithValue("@id", id);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpdateDeleteAgoAsync(DeleteAgoEntry entry)
    {
        using var conn = GetConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE deleteago
            SET
                days = COALESCE(@days, days),
                protect_mode = COALESCE(@protect, protect_mode)
            WHERE id = @id;
        ";

        cmd.Parameters.AddWithValue("@id", entry.Id);
        cmd.Parameters.AddWithValue("@days",
            entry.Days > 0 ? entry.Days : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@protect",
            string.IsNullOrWhiteSpace(entry.ProtectMode) ? (object)DBNull.Value : entry.ProtectMode);

        await cmd.ExecuteNonQueryAsync();
    }
}

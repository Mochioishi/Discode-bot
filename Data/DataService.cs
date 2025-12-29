// ------------------------------
// RoleGiveEntry の保存
// ------------------------------
public async Task AddRoleGiveAsync(RoleGiveEntry entry)
{
    // 絵文字を統一形式に変換
    entry.Emoji = NormalizeEmoji(entry.Emoji);

    using var conn = GetConnection();
    await conn.OpenAsync();

    var cmd = conn.CreateCommand();
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

// ------------------------------
// RoleGiveEntry の取得（ギルド単位）
// ------------------------------
public async Task<IEnumerable<RoleGiveEntry>> GetRoleGivesByGuildAsync(ulong guildId)
{
    var list = new List<RoleGiveEntry>();

    using var conn = GetConnection();
    await conn.OpenAsync();

    var cmd = conn.CreateCommand();
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
            Emoji = NormalizeEmoji(reader.GetString(5)) // ← 読み込み時も統一
        });
    }

    return list;
}

// ------------------------------
// RoleGiveEntry の取得（メッセージ単位）
// ------------------------------
public async Task<RoleGiveEntry?> GetRoleGiveByMessageAsync(ulong guildId, ulong channelId, ulong messageId)
{
    using var conn = GetConnection();
    await conn.OpenAsync();

    var cmd = conn.CreateCommand();
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

// ------------------------------
// RoleGiveEntry の削除
// ------------------------------
public async Task DeleteRoleGiveAsync(long id)
{
    using var conn = GetConnection();
    await conn.OpenAsync();

    var cmd = conn.CreateCommand();
    cmd.CommandText = @"DELETE FROM rolegive WHERE id = @id;";
    cmd.Parameters.AddWithValue("@id", id);

    await cmd.ExecuteNonQueryAsync();
}

// ------------------------------
// 絵文字の統一処理
// ------------------------------
private string NormalizeEmoji(string emoji)
{
    // Unicode 絵文字はそのまま
    if (!emoji.Contains(':'))
        return emoji;

    // すでに <:name:id> 形式ならそのまま
    if (emoji.StartsWith("<:") && emoji.EndsWith(">"))
        return emoji;

    // name:id → <:name:id> に変換
    return $"<{emoji}>";
}

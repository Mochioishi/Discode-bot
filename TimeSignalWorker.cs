// 現在の日本時間を取得
var jstNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time"));
string currentTime = jstNow.ToString("HHmm");

using var conn = new NpgsqlConnection(DbConfig.GetConnectionString());
await conn.OpenAsync();

// 現在時刻と一致する予約を検索
using var cmd = new NpgsqlCommand("SELECT * FROM scheduled_messages WHERE scheduled_time = @time", conn);
cmd.Parameters.AddWithValue("time", currentTime);

using var reader = await cmd.ExecuteReaderAsync();
while (await reader.ReadAsync())
{
    var channelId = ulong.Parse(reader.GetString(reader.GetOrdinal("channel_id")));
    var content = reader.GetString(reader.GetOrdinal("content"));
    var isEmbed = reader.GetBoolean(reader.GetOrdinal("is_embed"));
    var title = reader.IsDBNull(reader.GetOrdinal("embed_title")) ? null : reader.GetString(reader.GetOrdinal("embed_title"));

    var channel = _client.GetChannel(channelId) as IMessageChannel;
    if (channel != null)
    {
        if (isEmbed)
        {
            var embed = new EmbedBuilder().WithTitle(title).WithDescription(content).WithColor(Color.Blue).Build();
            await channel.SendMessageAsync(embed: embed);
        }
        else
        {
            await channel.SendMessageAsync(content);
        }
    }
}
// 送信後にその時刻のレコードを削除する処理を別途追加

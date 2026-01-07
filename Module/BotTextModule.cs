using Discord;
using Discord.Interactions;
using Npgsql;

public class BotTextModule : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("bottext", "メッセージを投稿または予約します")]
    public async Task HandleBotText(
        [Summary("content", "送信するテキスト内容")] string content,
        [Summary("is_embed", "埋め込み形式にするかどうか（デフォルト：False）")] bool isEmbed = false,
        [Summary("title", "埋め込み時のタイトル")] string? title = null,
        [Summary("time", "予約時間 (hhmm形式 / 例: 0830) 空白なら即時送信")] string? time = null
    )
    {
        // 時間指定がない場合は即時送信
        if (string.IsNullOrEmpty(time))
        {
            if (isEmbed)
            {
                var embed = new EmbedBuilder().WithTitle(title).WithDescription(content).WithColor(Color.Blue).Build();
                await ReplyAsync(embed: embed);
            }
            else
            {
                await ReplyAsync(content);
            }
            await RespondAsync("メッセージを送信しました。", ephemeral: true);
            return;
        }

        // 時間指定がある場合はDBに保存
        // 08:30 などの記号を削除して数字4桁にする
        string cleanTime = time.Replace(":", "");
        
        using var conn = new NpgsqlConnection(DbConfig.GetConnectionString());
        await conn.OpenAsync();
        using var cmd = new NpgsqlCommand(@"
            INSERT INTO scheduled_messages (guild_id, channel_id, content, is_embed, embed_title, scheduled_time) 
            VALUES (@gid, @cid, @txt, @emb, @ttl, @time)", conn);

        cmd.Parameters.AddWithValue("gid", Context.Guild.Id.ToString());
        cmd.Parameters.AddWithValue("cid", Context.Channel.Id.ToString());
        cmd.Parameters.AddWithValue("txt", content);
        cmd.Parameters.AddWithValue("emb", isEmbed);
        cmd.Parameters.AddWithValue("ttl", (object?)title ?? DBNull.Value);
        cmd.Parameters.AddWithValue("time", cleanTime);

        await cmd.ExecuteNonQueryAsync();
        await RespondAsync($"時刻 {cleanTime} にメッセージを予約しました。", ephemeral: true);
    }
}

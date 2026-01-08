using Discord;
using Discord.Interactions;
using Npgsql;
using System;
using System.Threading.Tasks;
using DiscordBot.Infrastructure;

namespace DiscordBot.Modules
{
    public class BotTextModule : InteractionModuleBase<SocketInteractionContext>
    {
        // データベース接続文字列の取得 (Railway用)
        private string GetConnectionString()
        {
            var url = Environment.GetEnvironmentVariable("DATABASE_URL");
            if (string.IsNullOrEmpty(url)) return "Host=localhost;Username=postgres;Password=password;Database=discord_bot";

            var uri = new Uri(url);
            var userInfo = uri.UserInfo.Split(':');

            return new NpgsqlConnectionStringBuilder
            {
                Host = uri.Host,
                Port = uri.Port,
                Username = userInfo[0],
                Password = userInfo[1],
                Database = uri.LocalPath.TrimStart('/'),
                SslMode = SslMode.Require,
                TrustServerCertificate = true
            }.ToString();
        }

        [SlashCommand("bottext", "メッセージを投稿または予約します")]
        public async Task HandleBotText(
            [Summary("content", "送信するテキスト内容")] string content,
            [Summary("is_embed", "埋め込み形式にするかどうか (デフォルト: False)")] bool isEmbed = false,
            [Summary("title", "埋め込み時のタイトル")] string? title = null,
            [Summary("time", "予約時間 (hhmm形式 / 例: 0830) 空白なら即時送信")] string? time = null
        )
        {
            // 時間指定がない場合は即時送信
            if (string.IsNullOrEmpty(time))
            {
                if (isEmbed)
                {
                    var embed = new EmbedBuilder()
                        .WithTitle(title)
                        .WithDescription(content)
                        .WithColor(Color.Blue)
                        .Build();
                    await RespondAsync(embed: embed);
                }
                else
                {
                    await RespondAsync(content);
                }
                return;
            }

            // --- 予約処理 (DB保存) ---
            try
            {
                string cleanTime = time.Replace(":", "").Replace(" ", "");
                if (cleanTime.Length != 4 || !int.TryParse(cleanTime, out _))
                {
                    await RespondAsync("時刻は 0830 や 21:00 のような4桁の形式で入力してください。", ephemeral: true);
                    return;
                }

                using var conn = new NpgsqlConnection(GetConnectionString());
                await conn.OpenAsync();

                // テーブル自動作成
                using var createTableCmd = new NpgsqlCommand(@"
                    CREATE TABLE IF NOT EXISTS scheduled_messages (
                        id SERIAL PRIMARY KEY,
                        guild_id TEXT NOT NULL,
                        channel_id TEXT NOT NULL,
                        content TEXT NOT NULL,
                        is_embed BOOLEAN DEFAULT FALSE,
                        embed_title TEXT,
                        scheduled_time TEXT NOT NULL
                    );", conn);
                await createTableCmd.ExecuteNonQueryAsync();

                using var insertCmd = new NpgsqlCommand(@"
                    INSERT INTO scheduled_messages (guild_id, channel_id, content, is_embed, embed_title, scheduled_time) 
                    VALUES (@gid, @cid, @txt, @emb, @ttl, @time)", conn);

                insertCmd.Parameters.AddWithValue("gid", Context.Guild.Id.ToString());
                insertCmd.Parameters.AddWithValue("cid", Context.Channel.Id.ToString());
                insertCmd.Parameters.AddWithValue("txt", content);
                insertCmd.Parameters.AddWithValue("emb", isEmbed);
                insertCmd.Parameters.AddWithValue("ttl", (object?)title ?? DBNull.Value);
                insertCmd.Parameters.AddWithValue("time", cleanTime);

                await insertCmd.ExecuteNonQueryAsync();
                await RespondAsync($"時刻 `{cleanTime[..2]}:{cleanTime[2..]}` にメッセージを予約しました。", ephemeral: true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] {ex.Message}");
                await RespondAsync("エラーが発生しました。DB接続を確認してください。", ephemeral: true);
            }
        }

        [SlashCommand("bottext_list", "予約されているメッセージの一覧を表示・削除します")]
        public async Task HandleBotTextList()
        {
            try
            {
                using var conn = new NpgsqlConnection(GetConnectionString());
                await conn.OpenAsync();

                using var cmd = new NpgsqlCommand(@"
                    SELECT id, channel_id, content, scheduled_time 
                    FROM scheduled_messages 
                    WHERE guild_id = @gid 
                    ORDER BY scheduled_time ASC LIMIT 5", conn);
                cmd.Parameters.AddWithValue("gid", Context.Guild.Id.ToString());

                using var reader = await cmd.ExecuteReaderAsync();
                
                var embed = new EmbedBuilder()
                    .WithTitle("📅 予約投稿一覧 (最新5件)")
                    .WithColor(Color.Green);

                var component = new ComponentBuilder();
                bool hasData = false;

                while (await reader.ReadAsync())
                {
                    hasData = true;
                    var id = reader.GetInt32(0);
                    var channelId = ulong.Parse(reader.GetString(1));
                    var content = reader.GetString(2);
                    var time = reader.GetString(3);

                    string shortContent = content.Length > 20 ? content[..20] + "..." : content;
                    embed.AddField($"{time[..2]}:{time[2..]} (ID: {id})", $"<#{channelId}>: {shortContent}");
                    
                    component.WithButton($"削除 {id}", $"del_bt:{id}", ButtonStyle.Danger);
                }

                if (!hasData)
                {
                    await RespondAsync("予約されているメッセージはありません。", ephemeral: true);
                }
                else
                {
                    await RespondAsync(embed: embed.Build(), components: component.Build(), ephemeral: true);
                }
            }
            catch (Exception ex)
            {
                await RespondAsync($"一覧取得エラー: {ex.Message}", ephemeral: true);
            }
        }

        // ボタン削除イベントの受け取り
        [ComponentInteraction("del_bt:*")]
        public async Task HandleDeleteButton(string id)
        {
            using var conn = new NpgsqlConnection(GetConnectionString());
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand("DELETE FROM scheduled_messages WHERE id = @id", conn);
            cmd.Parameters.AddWithValue("id", int.Parse(id));
            await cmd.ExecuteNonQueryAsync();

            await RespondAsync($"予約 ID:{id} を削除しました。", ephemeral: true);
        }
    }
}

using Discord;
using Discord.Interactions;
using Discord_bot.Infrastructure;
using Dapper;
using MySqlConnector;
using System.Text;

namespace Discord_bot.Module
{
    public class BotTextModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly DbConfig _db;

        public BotTextModule(DbConfig db)
        {
            _db = db;
        }

        [SlashCommand("bottext", "Botを喋らせます（時間指定で予約投稿）")]
        public async Task BotTextMain(
            [Summary("text", "送信する本文")] string text, 
            [Summary("time", "予約時間 (hh:mm形式 / 未入力なら即時送信)")] string? time = null, 
            [Summary("is_embed", "埋め込み形式にするか")] bool is_embed = true,
            [Summary("title", "埋め込み時のタイトル")] string title = "お知らせ")
        {
            // 1. 時間指定がない場合は「即時送信」
            if (string.IsNullOrWhiteSpace(time))
            {
                if (is_embed)
                {
                    var embed = new EmbedBuilder()
                        .WithTitle(title)
                        .WithDescription(text)
                        .WithColor(Color.Blue)
                        .WithCurrentTimestamp()
                        .Build();
                    await Context.Channel.SendMessageAsync(embed: embed);
                }
                else
                {
                    await Context.Channel.SendMessageAsync(text);
                }

                await RespondAsync("✅ メッセージを即時送信しました。", ephemeral: true);
                return;
            }

            // 2. 時間指定がある場合は「DBに予約保存」
            using var conn = _db.GetConnection();
            const string sql = @"
                INSERT INTO BotTextSchedules (Text, Title, ScheduledTime, IsEmbed, ChannelId, GuildId) 
                VALUES (@text, @title, @time, @is_embed, @channelId, @guildId)";

            await conn.ExecuteAsync(sql, new
            {
                text,
                title,
                time,
                is_embed,
                channelId = Context.Channel.Id,
                guildId = Context.Guild.Id
            });

            await RespondAsync($"📅 `{time}` に予約を追加しました。\n内容: {text.Substring(0, Math.Min(text.Length, 20))}...", ephemeral: true);
        }

        [SlashCommand("bottext_list", "予約一覧を表示・削除します")]
        public async Task List()
        {
            using var conn = _db.GetConnection();
            const string sql = "SELECT Id, ScheduledTime, Title FROM BotTextSchedules WHERE GuildId = @guildId ORDER BY ScheduledTime";
            
            var schedules = (await conn.QueryAsync<(int Id, string Time, string Title)>(sql, new { guildId = Context.Guild.Id })).ToList();

            if (!schedules.Any())
            {
                await RespondAsync("現在予約されている投稿はありません。", ephemeral: true);
                return;
            }

            var sb = new StringBuilder().AppendLine("【現在の予約一覧】");
            var builder = new ComponentBuilder();

            foreach (var item in schedules)
            {
                sb.AppendLine($"`{item.Time}` - {item.Title}");
                // ボタンIDを識別しやすく設定
                builder.WithButton($"削除: {item.Time}", $"bt_del_{item.Id}", ButtonStyle.Danger);
            }

            await RespondAsync(sb.ToString(), components: builder.Build(), ephemeral: true);
        }

        [ComponentInteraction("bt_del_*")]
        public async Task DeleteButtonHandler(string id)
        {
            using var conn = _db.GetConnection();
            const string sql = "DELETE FROM BotTextSchedules WHERE Id = @id";
            
            await conn.ExecuteAsync(sql, new { id = int.Parse(id) });

            await RespondAsync("✅ 予約を削除しました。", ephemeral: true);
        }
    }
}

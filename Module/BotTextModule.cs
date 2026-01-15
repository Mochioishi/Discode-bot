using Discord;
using Discord.Interactions;
using Discord_bot.Infrastructure;
using Dapper;

namespace Discord_bot.Module
{
    public class BotTextModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly DbConfig _db;
        public BotTextModule(DbConfig db) => _db = db;

        [SlashCommand("bottext", "Botに発言させます（時刻指定で予約可能）")]
        public async Task SetBotText(
            [Summary("text", "送信する本文")] string text,
            [Summary("time", "送信時刻 (HH:mm) 未記入で即時送信")] string time = "",
            [Summary("title", "埋め込み時のタイトル")] string title = "",
            [Summary("is_embed", "埋め込みメッセージにするか")] bool isEmbed = false)
        {
            // 1. まず「考え中...」の状態にする（タイムアウト防止）
            await DeferAsync(ephemeral: true);

            try
            {
                // 2. 時刻(time)が入力されていない場合は即時送信
                if (string.IsNullOrWhiteSpace(time))
                {
                    if (isEmbed)
                    {
                        var embed = new EmbedBuilder()
                            .WithTitle(string.IsNullOrEmpty(title) ? null : title)
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

                    await FollowupAsync("✅ メッセージを即時送信しました。", ephemeral: true);
                    return;
                }

                // 3. 時刻が入力されている場合は予約（データベース保存）
                using var conn = _db.GetConnection();
                const string sql = @"
                    INSERT INTO BotTextSchedules (Text, Title, ScheduledTime, IsEmbed, ChannelId, GuildId) 
                    VALUES (@txt, @ttl, @time, @emb, @cid, @gid)";

                await conn.ExecuteAsync(sql, new { 
                    txt = text, 
                    ttl = title, 
                    time = time, 
                    emb = isEmbed, 
                    cid = (long)Context.Channel.Id, 
                    gid = (long)Context.Guild.Id 
                });

                string type = isEmbed ? "埋め込み" : "通常";
                await FollowupAsync($"✅ {time} に{type}メッセージを予約しました。", ephemeral: true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BotText Error] {ex.Message}");
                await FollowupAsync($"❌ エラーが発生しました: {ex.Message}", ephemeral: true);
            }
        }
    }
}

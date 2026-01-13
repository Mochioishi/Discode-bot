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

        [SlashCommand("bottext", "予約投稿を設定します")]
        public async Task SetBotText(
            [Summary("text", "送信する本文")] string text,
            [Summary("time", "送信時刻 (HH:mm)")] string time,
            [Summary("title", "埋め込み時のタイトル")] string title = "",
            [Summary("is_embed", "埋め込みメッセージにするか (デフォルト: False)")] bool isEmbed = false)
        {
            using var conn = _db.GetConnection();
            const string sql = @"
                INSERT INTO BotTextSchedules (Text, Title, ScheduledTime, IsEmbed, ChannelId, GuildId) 
                VALUES (@txt, @ttl, @time, @emb, @cid, @gid)";

            await conn.ExecuteAsync(sql, new { 
                txt = text, ttl = title, time = time, emb = isEmbed, 
                cid = Context.Channel.Id, gid = Context.Guild.Id 
            });

            string type = isEmbed ? "埋め込み" : "通常";
            await RespondAsync($"✅ {time} に{type}メッセージを予約しました。", ephemeral: true);
        }
    }
}

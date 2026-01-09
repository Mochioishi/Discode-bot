using Discord;
using Discord.Interactions;
using DiscordBot.Infrastructure;
using Npgsql;
using System;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Modules
{
    public class BotTextModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly string _conn;
        public BotTextModule() => _conn = DbConfig.GetConnectionString();

        // --- 1. 予約の追加 (以前の add コマンド) ---
        [SlashCommand("bottext_add", "新しい予約投稿を追加します")]
        public async Task AddSchedule(
            [Summary("text", "表示したいメッセージ内容")] string text, 
            [Summary("time", "投稿時刻 (例: 08:30)")] string time,
            [Summary("channel", "投稿先のチャンネル")] ITextChannel channel,
            [Summary("title", "カードの見出し")] string title = "お知らせ",
            [Summary("show_time", "時刻を表示するか")] bool showTime = true)
        {
            try
            {
                using var conn = new NpgsqlConnection(_conn);
                await conn.OpenAsync();
                
                // テーブルに ChannelId を追加した設計
                var sql = @"INSERT INTO ""BotTextSchedules"" (""Text"", ""Title"", ""ScheduledTime"", ""ShowTime"", ""ChannelId"") 
                            VALUES (@txt, @ttl, @tm, @st, @cid)";
                
                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("txt", text);
                cmd.Parameters.AddWithValue("ttl", title);
                cmd.Parameters.AddWithValue("tm", time);
                cmd.Parameters.AddWithValue("st", showTime);
                cmd.Parameters.AddWithValue("cid", channel.Id.ToString());
                
                await cmd.ExecuteNonQueryAsync();
                await RespondAsync($"✅ <#{channel.Id}> へ {time} に投稿する予約を追加しました。", ephemeral: true);
            }
            catch (Exception ex)
            {
                await RespondAsync($"⚠️ エラー: {ex.Message}", ephemeral: true);
            }
        }

        // --- 2. 予約の一覧と削除ボタン (以前の list 機能) ---
        [SlashCommand("bottext_list", "予約一覧を表示し、ボタンで削除できます")]
        public async Task ListSchedules()
        {
            using var conn = new NpgsqlConnection(_conn);
            await conn.OpenAsync();
            
            var sql = "SELECT \"Id\", \"ScheduledTime\", \"Title\", \"ChannelId\" FROM \"BotTextSchedules\" ORDER BY \"ScheduledTime\"";
            using var cmd = new NpgsqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();

            var builder = new ComponentBuilder();
            var sb = new StringBuilder().AppendLine("【現在の予約投稿一覧】");

            int count = 0;
            while (await reader.ReadAsync())
            {
                int id = reader.GetInt32(0);
                string time = reader.GetString(1);
                string title = reader.GetString(2);
                string cid = reader.GetString(3);

                sb.AppendLine($"`{time}` - **{title}** (<#{cid}>)");
                
                // 削除ボタンを生成
                builder.WithButton($"削除 ({time})", $"bt_del_{id}", ButtonStyle.Danger);
                count++;
            }

            if (count == 0) await RespondAsync("現在、予約されている投稿はありません。", ephemeral: true);
            else await RespondAsync(sb.ToString(), components: builder.Build(), ephemeral: true);
        }

        // --- 3. 即時送信 (以前の表示機能) ---
        [SlashCommand("bottext_send", "保存せずに、今すぐEmbedメッセージを送信します")]
        public async Task SendNow(
            string text, 
            ITextChannel channel, 
            string title = "お知らせ", 
            bool time = true)
        {
            var eb = new EmbedBuilder()
                .WithTitle(title)
                .WithDescription(text)
                .WithColor(new Color(0x3498db));

            if (time) eb.WithCurrentTimestamp();

            await channel.SendMessageAsync(embed: eb.Build());
            await RespondAsync("🚀 メッセージを送信しました。", ephemeral: true);
        }
    }
}

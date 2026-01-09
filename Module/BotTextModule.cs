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
        private readonly string _connectionString;

        public BotTextModule()
        {
            _connectionString = DbConfig.GetConnectionString();
        }

        [SlashCommand("bottext_add", "予約投稿を追加します")]
        public async Task AddSchedule(
            [Summary("text", "表示したいメッセージ")] string text, 
            [Summary("time", "投稿時刻 (例: 08:30)")] string time,
            [Summary("title", "カードの見出し")] string title = "お知らせ",
            [Summary("show_time", "時刻を表示するか")] bool showTime = true)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();
                var sql = "INSERT INTO \"BotTextSchedules\" (\"Text\", \"Title\", \"ScheduledTime\", \"ShowTime\") VALUES (@txt, @ttl, @tm, @st)";
                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("txt", text);
                cmd.Parameters.AddWithValue("ttl", title);
                cmd.Parameters.AddWithValue("tm", time);
                cmd.Parameters.AddWithValue("st", showTime);
                await cmd.ExecuteNonQueryAsync();

                await RespondAsync($"✅ {time} に 「{title}」 を予約しました。", ephemeral: true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Add Error]: {ex.Message}");
                await RespondAsync("⚠️ 予約の保存に失敗しました。", ephemeral: true);
            }
        }

        [SlashCommand("bottext_list", "予約一覧を表示し、ボタンで削除できます")]
        public async Task ListSchedules()
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            var sql = "SELECT \"Id\", \"ScheduledTime\", \"Title\" FROM \"BotTextSchedules\" ORDER BY \"ScheduledTime\"";
            using var cmd = new NpgsqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();

            var builder = new ComponentBuilder();
            var sb = new StringBuilder();
            sb.AppendLine("【現在の予約投稿一覧】");

            int count = 0;
            while (await reader.ReadAsync())
            {
                int id = reader.GetInt32(0);
                string time = reader.GetString(1);
                string title = reader.GetString(2);

                sb.AppendLine($"`{time}` - **{title}**");
                
                // ボタンのCustomIdにIDを埋め込む (例: bt_del_5)
                builder.WithButton($"削除 ({time})", $"bt_del_{id}", ButtonStyle.Danger);
                count++;
            }

            if (count == 0)
            {
                await RespondAsync("予約はありません。", ephemeral: true);
            }
            else
            {
                // リストとボタンを一緒に送信
                await RespondAsync(sb.ToString(), components: builder.Build(), ephemeral: true);
            }
        }
    }
}

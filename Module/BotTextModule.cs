using Discord;
using Discord.Interactions;
using DiscordBot.Infrastructure;
using Npgsql;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Modules
{
    public class BotTextModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly string _conn;
        public BotTextModule() => _conn = DbConfig.GetConnectionString();

        [SlashCommand("bottext_add", "予約投稿を追加")]
        public async Task Add([Summary("text")] string text, [Summary("time")] string time, [Summary("title")] string title = "お知らせ", [Summary("show_time")] bool showTime = true)
        {
            using var conn = new NpgsqlConnection(_conn);
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand("INSERT INTO \"BotTextSchedules\" (\"Text\", \"Title\", \"ScheduledTime\", \"ShowTime\") VALUES (@txt, @ttl, @tm, @st)", conn);
            cmd.Parameters.AddWithValue("txt", text);
            cmd.Parameters.AddWithValue("ttl", title);
            cmd.Parameters.AddWithValue("tm", time);
            cmd.Parameters.AddWithValue("st", showTime);
            await cmd.ExecuteNonQueryAsync();
            await RespondAsync($"✅ {time} に予約しました。", ephemeral: true);
        }

        [SlashCommand("bottext_list", "予約一覧と削除")]
        public async Task List()
        {
            using var conn = new NpgsqlConnection(_conn);
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand("SELECT \"Id\", \"ScheduledTime\", \"Title\" FROM \"BotTextSchedules\" ORDER BY \"ScheduledTime\"", conn);
            using var reader = await cmd.ExecuteReaderAsync();

            var builder = new ComponentBuilder();
            var sb = new StringBuilder().AppendLine("【予約一覧】");
            int count = 0;
            while (await reader.ReadAsync())
            {
                int id = reader.GetInt32(0);
                string time = reader.GetString(1);
                sb.AppendLine($"`{time}` - {reader.GetString(2)}");
                builder.WithButton($"削除 ({time})", $"bt_del_{id}", ButtonStyle.Danger);
                count++;
            }
            if (count == 0) await RespondAsync("予約なし", ephemeral: true);
            else await RespondAsync(sb.ToString(), components: builder.Build(), ephemeral: true);
        }
    }
}

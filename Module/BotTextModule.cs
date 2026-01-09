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

        [SlashCommand("bottext_add", "予約追加")]
        public async Task Add(string text, string time, ITextChannel channel, string title = "お知らせ", bool show_time = true)
        {
            using var conn = new NpgsqlConnection(_conn); await conn.OpenAsync();
            using var cmd = new NpgsqlCommand(@"INSERT INTO ""BotTextSchedules"" (""Text"", ""Title"", ""ScheduledTime"", ""ShowTime"", ""ChannelId"") VALUES (@txt, @ttl, @tm, @st, @cid)", conn);
            cmd.Parameters.AddWithValue("txt", text); cmd.Parameters.AddWithValue("ttl", title);
            cmd.Parameters.AddWithValue("tm", time); cmd.Parameters.AddWithValue("st", show_time);
            cmd.Parameters.AddWithValue("cid", channel.Id.ToString());
            await cmd.ExecuteNonQueryAsync();
            await RespondAsync("✅ 予約しました", ephemeral: true);
        }

        [SlashCommand("bottext_list", "予約一覧")]
        public async Task List()
        {
            using var conn = new NpgsqlConnection(_conn); await conn.OpenAsync();
            using var cmd = new NpgsqlCommand(@"SELECT ""Id"", ""ScheduledTime"", ""Title"" FROM ""BotTextSchedules"" ORDER BY ""ScheduledTime""", conn);
            using var r = await cmd.ExecuteReaderAsync();
            var b = new ComponentBuilder(); var sb = new StringBuilder().AppendLine("【予約一覧】");
            while (await r.ReadAsync()) {
                sb.AppendLine($"`{r.GetString(1)}` - {r.GetString(2)}");
                b.WithButton($"削除 {r.GetString(1)}", $"bt_del_{r.GetInt32(0)}", ButtonStyle.Danger);
            }
            await RespondAsync(sb.ToString(), components: b.Build(), ephemeral: true);
        }
    }
}

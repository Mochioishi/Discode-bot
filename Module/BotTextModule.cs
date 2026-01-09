using Discord;
using Discord.Interactions;
using DiscordBot.Infrastructure;
using Npgsql;
using System;
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

        [SlashCommand("bottext", "テキストを表示・保存します")]
        public async Task BotTextCommand(
            [Summary("text", "表示・保存したいメッセージ")] string text = "", 
            [Summary("embed", "カード形式で表示するか")] bool embed = true,
            [Summary("title", "カードの見出し")] string title = "お知らせ",
            [Summary("time", "時刻を表示するか")] bool time = true)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                // embed引数を渡して表示処理へ
                await ShowTextAsync(embed);
            }
            else
            {
                // 保存処理
                await SaveTextAsync(text, title, time);
                await RespondAsync("✅ 保存しました。引数なしで実行すると表示できます。", ephemeral: true);
            }
        }

        private async Task ShowTextAsync(bool useEmbed)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new NpgsqlCommand("SELECT \"Text\", \"Title\", \"ShowTime\" FROM \"BotTexts\" LIMIT 1", conn);
                using var reader = await cmd.ExecuteReaderAsync();
                
                if (await reader.ReadAsync())
                {
                    var savedText = reader.GetString(0);
                    var title = reader.GetString(1);
                    var showTime = reader.GetBoolean(2);

                    await RespondAsync("表示します...", ephemeral: true);

                    if (useEmbed)
                    {
                        // --- カード形式 (Embed) ---
                        var eb = new EmbedBuilder()
                            .WithTitle(title)
                            .WithDescription(savedText)
                            .WithColor(new Color(0x3498db));

                        if (showTime) eb.WithCurrentTimestamp();

                        await Context.Channel.SendMessageAsync(embed: eb.Build());
                    }
                    else
                    {
                        // --- 通常テキスト形式 ---
                        string msg = $"**{title}**\n{savedText}";
                        if (showTime) msg += $"\n*(送信時刻: {DateTime.Now:HH:mm})*";
                        
                        await Context.Channel.SendMessageAsync(msg);
                    }
                }
                else
                {
                    await RespondAsync("❌ 保存データがありません。", ephemeral: true);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BotText Error]: {ex.Message}");
                await RespondAsync("⚠️ エラーが発生しました。", ephemeral: true);
            }
        }

        private async Task SaveTextAsync(string text, string title, bool showTime)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            using (var delCmd = new NpgsqlCommand("DELETE FROM \"BotTexts\"", conn)) await delCmd.ExecuteNonQueryAsync();
            
            using var cmd = new NpgsqlCommand(
                "INSERT INTO \"BotTexts\" (\"Text\", \"Title\", \"ShowTime\") VALUES (@txt, @ttl, @st)", conn);
            cmd.Parameters.AddWithValue("txt", text);
            cmd.Parameters.AddWithValue("ttl", title);
            cmd.Parameters.AddWithValue("st", showTime);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}

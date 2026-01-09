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
            // インフラ層から接続文字列を取得するように統一
            _connectionString = DbConfig.GetConnectionString();
        }

        [SlashCommand("bottext", "テキストをボットに保存・表示させます")]
        public async Task BotTextCommand(string text = "")
        {
            // 空の場合はDBから取得して表示、文字がある場合は保存
            if (string.IsNullOrWhiteSpace(text))
            {
                await ShowTextAsync();
            }
            else
            {
                await SaveTextAsync(text);
            }
        }

        private async Task ShowTextAsync()
        {
            string savedText = "保存されたテキストはありません。";

            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();

                // テーブル名とカラム名は引用符で囲む設計に統一
                using var cmd = new NpgsqlCommand("SELECT \"Content\" FROM \"BotTexts\" LIMIT 1", conn);
                var result = await cmd.ExecuteScalarAsync();
                
                if (result != null)
                {
                    savedText = result.ToString() ?? savedText;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BotText Error]: {ex.Message}");
                savedText = "エラーが発生しました。";
            }

            // 【訂正箇所】Reply（返信）ではなく、独立したメッセージとして送信
            // RespondAsyncだと左上にアイコンが出ますが、SendMessageAsyncなら出ません。
            await RespondAsync("取得しました", ephemeral: true); // 実行者本人にのみ「取得しました」と通知
            await Context.Channel.SendMessageAsync(savedText); // チャンネルに独立して投稿
        }

        private async Task SaveTextAsync(string text)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();

                // テーブルがなければ作成（初期化に含めても良いですが、ここでも担保）
                var createSql = "CREATE TABLE IF NOT EXISTS \"BotTexts\" (\"Content\" TEXT)";
                using (var createCmd = new NpgsqlCommand(createSql, conn)) await createCmd.ExecuteNonQueryAsync();

                // 既存のデータを消して新しく保存
                using (var delCmd = new NpgsqlCommand("DELETE FROM \"BotTexts\"", conn)) await delCmd.ExecuteNonQueryAsync();
                
                using var cmd = new NpgsqlCommand("INSERT INTO \"BotTexts\" (\"Content\") VALUES (@txt)", conn);
                cmd.Parameters.AddWithValue("txt", text);
                await cmd.ExecuteNonQueryAsync();

                await RespondAsync($"テキストを保存しました： {text}", ephemeral: true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BotText Save Error]: {ex.Message}");
                await RespondAsync("保存に失敗しました。", ephemeral: true);
            }
        }
    }
}

using Discord;
using Discord.Interactions;
using DiscordBot.Infrastructure;
using Npgsql;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DiscordBot.Modules
{
    public class PrskModule : InteractionModuleBase<SocketInteractionContext>
    {
        private string GetConnectionString()
        {
            var url = Environment.GetEnvironmentVariable("DATABASE_URL");
            if (string.IsNullOrEmpty(url)) return "Host=localhost;Username=postgres;Password=password;Database=discord_bot";
            var uri = new Uri(url);
            var userInfo = uri.UserInfo.Split(':');
            return new NpgsqlConnectionStringBuilder
            {
                Host = uri.Host, Port = uri.Port, Username = userInfo[0], Password = userInfo[1],
                Database = uri.LocalPath.TrimStart('/'), SslMode = SslMode.Require, TrustServerCertificate = true
            }.ToString();
        }

        [SlashCommand("prsk_roomid", "部屋番号の監視とチャンネル名変更を設定します")]
        public async Task SetPrskMonitor(
            [Summary("monitor", "番号を書き込む監視チャンネル")] ITextChannel monitor,
            [Summary("target", "名前が書き換わる対象チャンネル")] ITextChannel target,
            [Summary("template", "名前の形式（例: 部屋【roomid】）")] string template
        )
        {
            if (!template.Contains("【roomid】"))
            {
                await RespondAsync("テンプレートには必ず `【roomid】` という文字列を含めてください。", ephemeral: true);
                return;
            }

            try
            {
                using var conn = new NpgsqlConnection(GetConnectionString());
                await conn.OpenAsync();

                // テーブル作成
                using var createTableCmd = new NpgsqlCommand(@"
                    CREATE TABLE IF NOT EXISTS prsk_settings (
                        monitor_channel_id TEXT PRIMARY KEY,
                        target_channel_id TEXT NOT NULL,
                        original_name TEXT NOT NULL,
                        game_type TEXT DEFAULT 'prsk'
                    );", conn);
                await createTableCmd.ExecuteNonQueryAsync();

                using var cmd = new NpgsqlCommand(@"
                    INSERT INTO prsk_settings (monitor_channel_id, target_channel_id, original_name)
                    VALUES (@mcid, @tcid, @name)
                    ON CONFLICT (monitor_channel_id) 
                    DO UPDATE SET target_channel_id = EXCLUDED.target_channel_id, original_name = EXCLUDED.original_name;", conn);

                cmd.Parameters.AddWithValue("mcid", monitor.Id.ToString());
                cmd.Parameters.AddWithValue("tcid", target.Id.ToString());
                cmd.Parameters.AddWithValue("name", template);

                await cmd.ExecuteNonQueryAsync();
                await RespondAsync($"✅ 監視を開始しました。\n監視: <#{monitor.Id}>\n対象: <#{target.Id}>\n形式: `{template}`", ephemeral: true);
            }
            catch (Exception ex)
            {
                await RespondAsync($"エラーが発生しました: {ex.Message}", ephemeral: true);
            }
        }

        [SlashCommand("prsk_roomid_list", "プロセカ部屋番号監視の一覧を表示します")]
        public async Task ListPrsk()
        {
            using var conn = new NpgsqlConnection(GetConnectionString());
            await conn.OpenAsync();

            using var cmd = new NpgsqlCommand("SELECT monitor_channel_id, target_channel_id, original_name FROM prsk_settings", conn);
            using var reader = await cmd.ExecuteReaderAsync();

            var embed = new EmbedBuilder().WithTitle("🎮 プロセカ監視一覧").WithColor(Color.Blue);
            var component = new ComponentBuilder();
            bool hasData = false;

            while (await reader.ReadAsync())
            {
                hasData = true;
                var mcid = reader.GetString(0);
                var tcid = reader.GetString(1);
                var name = reader.GetString(2);

                embed.AddField($"監視: <#{mcid}>", $"➡ 対象: <#{tcid}>\n形式: `{name}`");
                component.WithButton($"解除 {mcid}", $"stop_prsk:{mcid}", ButtonStyle.Danger);
            }

            if (!hasData) await RespondAsync("現在、有効な監視設定はありません。", ephemeral: true);
            else await RespondAsync(embed: embed.Build(), components: component.Build(), ephemeral: true);
        }

        [ComponentInteraction("stop_prsk:*")]
        public async Task StopPrsk(string mcid)
        {
            using var conn = new NpgsqlConnection(GetConnectionString());
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand("DELETE FROM prsk_settings WHERE monitor_channel_id = @mcid", conn);
            cmd.Parameters.AddWithValue("mcid", mcid);
            await cmd.ExecuteNonQueryAsync();

            await RespondAsync("監視設定を解除しました。", ephemeral: true);
        }
    }
}

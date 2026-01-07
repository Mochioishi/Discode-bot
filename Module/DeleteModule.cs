using Discord;
using Discord.Interactions;
using Npgsql;
using System;
using System.Threading.Tasks;

namespace DiscordBot.Modules
{
    public class DeleteModule : InteractionModuleBase<SocketInteractionContext>
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

        // 保護ルールの選択肢を定義
        public enum ProtectionType
        {
            [ChoiceDisplay("なし (すべて削除)")] None,
            [ChoiceDisplay("画像付きを保護")] Image,
            [ChoiceDisplay("リアクション付きを保護")] Reaction,
            [ChoiceDisplay("画像またはリアクション付きを保護")] Both
        }

        [SlashCommand("deleteago", "このチャンネルの自動削除を設定します")]
        public async Task SetAutoPurge(
            [Summary("days", "何日経過したメッセージを削除するか")] int days,
            [Summary("protection", "保護する対象 (指定しない場合は『なし』)")] ProtectionType protection = ProtectionType.None
        )
        {
            try
            {
                using var conn = new NpgsqlConnection(GetConnectionString());
                await conn.OpenAsync();

                // テーブル作成
                using var createTableCmd = new NpgsqlCommand(@"
                    CREATE TABLE IF NOT EXISTS auto_purge_settings (
                        channel_id TEXT PRIMARY KEY,
                        days_ago INTEGER NOT NULL,
                        protection_type TEXT NOT NULL
                    );", conn);
                await createTableCmd.ExecuteNonQueryAsync();

                // UPSERT (あれば更新、なければ挿入)
                using var cmd = new NpgsqlCommand(@"
                    INSERT INTO auto_purge_settings (channel_id, days_ago, protection_type)
                    VALUES (@cid, @days, @prot)
                    ON CONFLICT (channel_id) 
                    DO UPDATE SET days_ago = EXCLUDED.days_ago, protection_type = EXCLUDED.protection_type;", conn);

                cmd.Parameters.AddWithValue("cid", Context.Channel.Id.ToString());
                cmd.Parameters.AddWithValue("days", days);
                cmd.Parameters.AddWithValue("prot", protection.ToString());

                await cmd.ExecuteNonQueryAsync();

                await RespondAsync($"このチャンネルの自動削除を設定しました。\n" +
                                   $"設定: {days}日経過後に削除\n" +
                                   $"保護: {protection}", ephemeral: true);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                await RespondAsync("設定中にエラーが発生しました。", ephemeral: true);
            }
        }

        [SlashCommand("deleteago_list", "自動削除の設定一覧を表示します")]
        public async Task ListAutoPurge()
        {
            using var conn = new NpgsqlConnection(GetConnectionString());
            await conn.OpenAsync();

            using var cmd = new NpgsqlCommand("SELECT channel_id, days_ago, protection_type FROM auto_purge_settings", conn);
            using var reader = await cmd.ExecuteReaderAsync();

            var embed = new EmbedBuilder().WithTitle("🧹 自動削除設定一覧").WithColor(Color.Orange);
            var component = new ComponentBuilder();
            bool hasData = false;

            while (await reader.ReadAsync())
            {
                hasData = true;
                var cid = reader.GetString(0);
                var days = reader.GetInt32(1);
                var prot = reader.GetString(2);

                embed.AddField($"チャンネル: <#{cid}>", $"{days}日後に削除 (保護: {prot})");
                component.WithButton($"解除 <#{cid}>", $"stop_purge:{cid}", ButtonStyle.Danger);
            }

            if (!hasData)
            {
                await RespondAsync("有効な自動削除設定はありません。", ephemeral: true);
            }
            else
            {
                await RespondAsync(embed: embed.Build(), components: component.Build(), ephemeral: true);
            }
        }

        [ComponentInteraction("stop_purge:*")]
        public async Task StopPurge(string cid)
        {
            using var conn = new NpgsqlConnection(GetConnectionString());
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand("DELETE FROM auto_purge_settings WHERE channel_id = @cid", conn);
            cmd.Parameters.AddWithValue("cid", cid);
            await cmd.ExecuteNonQueryAsync();

            await RespondAsync($"<#{cid}> の自動削除設定を解除しました。", ephemeral: true);
        }
    }
}

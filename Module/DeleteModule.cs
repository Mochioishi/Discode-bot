using Discord;
using Discord.Interactions;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DiscordBot.Modules
{
    public class DeleteModule : InteractionModuleBase<SocketInteractionContext>
    {
        // 範囲削除用の開始地点を一時保持（ユーザーID, メッセージID）
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<ulong, ulong> _startPoints = new();

        // データベース接続文字列の取得
        private string GetConnectionString()
        {
            var url = Environment.GetEnvironmentVariable("DATABASE_URL");
            if (string.IsNullOrEmpty(url)) return "Host=localhost;Username=postgres;Password=password;Database=discord_bot";

            var uri = new Uri(url);
            var userInfo = uri.UserInfo.Split(':');

            return new NpgsqlConnectionStringBuilder
            {
                Host = uri.Host,
                Port = uri.Port,
                Username = userInfo[0],
                Password = userInfo[1],
                Database = uri.LocalPath.TrimStart('/'),
                SslMode = SslMode.Require,
                TrustServerCertificate = true
            }.ToString();
        }

        // --- 1. 自動削除 (deleteago) ---

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
            [Summary("protection", "保護対象 (指定なしで『なし』)")] ProtectionType protection = ProtectionType.None
        )
        {
            try
            {
                using var conn = new NpgsqlConnection(GetConnectionString());
                await conn.OpenAsync();

                using var createTableCmd = new NpgsqlCommand(@"
                    CREATE TABLE IF NOT EXISTS auto_purge_settings (
                        channel_id TEXT PRIMARY KEY,
                        days_ago INTEGER NOT NULL,
                        protection_type TEXT NOT NULL
                    );", conn);
                await createTableCmd.ExecuteNonQueryAsync();

                using var cmd = new NpgsqlCommand(@"
                    INSERT INTO auto_purge_settings (channel_id, days_ago, protection_type)
                    VALUES (@cid, @days, @prot)
                    ON CONFLICT (channel_id) 
                    DO UPDATE SET days_ago = EXCLUDED.days_ago, protection_type = EXCLUDED.protection_type;", conn);

                cmd.Parameters.AddWithValue("cid", Context.Channel.Id.ToString());
                cmd.Parameters.AddWithValue("days", days);
                cmd.Parameters.AddWithValue("prot", protection.ToString());

                await cmd.ExecuteNonQueryAsync();
                await RespondAsync($"✅ 自動削除を設定: **{days}日経過後**\n🛡️ 保護ルール: **{protection}**", ephemeral: true);
            }
            catch (Exception ex)
            {
                await RespondAsync($"エラーが発生しました: {ex.Message}", ephemeral: true);
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
                embed.AddField($"チャンネル: <#{cid}>", $"{reader.GetInt32(1)}日後削除 (保護: {reader.GetString(2)})");
                component.WithButton($"解除 {cid}", $"stop_purge:{cid}", ButtonStyle.Danger);
            }

            if (!hasData) await RespondAsync("有効な設定はありません。", ephemeral: true);
            else await RespondAsync(embed: embed.Build(), components: component.Build(), ephemeral: true);
        }

        [ComponentInteraction("stop_purge:*")]
        public async Task StopPurge(string cid)
        {
            using var conn = new NpgsqlConnection(GetConnectionString());
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand("DELETE FROM auto_purge_settings WHERE channel_id = @cid", conn);
            cmd.Parameters.AddWithValue("cid", cid);
            await cmd.ExecuteNonQueryAsync();
            await RespondAsync("設定を解除しました。", ephemeral: true);
        }

        // --- 2. 範囲削除 (Delete_Range) ---

        [MessageCommand("開始地点に設定")]
        public async Task SetStartPoint(IMessage message)
        {
            _startPoints[Context.User.Id] = message.Id;
            await RespondAsync("📍 **開始地点**を記憶しました。終了地点で `[ここで範囲削除]` を選んでください。", ephemeral: true);
        }

        [MessageCommand("ここで範囲削除")]
        public async Task DeleteRangeMenu(IMessage message)
        {
            if (!_startPoints.TryGetValue(Context.User.Id, out var startId))
            {
                await RespondAsync("❌ 開始地点が設定されていません。先に `[開始地点に設定]` を実行してください。", ephemeral: true);
                return;
            }

            var menu = new SelectMenuBuilder()
                .WithPlaceholder("適用する保護ルールを選択")
                .WithCustomId($"range_exec:{startId}:{message.Id}")
                .AddOption("なし (すべて削除)", "None")
                .AddOption("画像付きを保護", "Image")
                .AddOption("リアクション付きを保護", "Reaction")
                .AddOption("画像またはリアクション付きを保護", "Both");

            await RespondAsync("範囲削除を実行します。ルールを選んでください：", 
                components: new ComponentBuilder().WithSelectMenu(menu).Build(), ephemeral: true);
        }

        [ComponentInteraction("range_exec:*:*")]
        public async Task ExecuteRangeDelete(string sId, string eId, string[] choice)
        {
            await DeferAsync(ephemeral: true);
            ulong start = ulong.Parse(sId);
            ulong end = ulong.Parse(eId);
            string prot = choice[0];

            var first = Math.Min(start, end);
            var last = Math.Max(start, end);

            // メッセージ取得
            var msgs = await Context.Channel.GetMessagesAsync(first, Direction.After, 100).FlattenAsync();
            var startMsg = await Context.Channel.GetMessageAsync(first);

            var targets = new List<IMessage>();
            if (startMsg != null) targets.Add(startMsg);
            foreach (var m in msgs) { targets.Add(m); if (m.Id == last) break; }

            // 保護フィルタリング
            var toDelete = targets.Where(m => {
                bool hasImg = m.Attachments.Any();
                bool hasReac = m.Reactions.Any();
                return prot switch {
                    "Image" => !hasImg,
                    "Reaction" => !hasReac,
                    "Both" => !hasImg && !hasReac,
                    _ => true
                };
            }).ToList();

            if (Context.Channel is ITextChannel txtChannel && toDelete.Any())
            {
                await txtChannel.DeleteMessagesAsync(toDelete);
            }

            _startPoints.TryRemove(Context.User.Id, out _);
            await FollowupAsync($"🗑️ **{toDelete.Count}件**削除しました。(保護: {targets.Count - toDelete.Count}件)", ephemeral: true);
        }
    }
}

using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Infrastructure;
using Npgsql;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DiscordBot.Modules
{
    public class PrskModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly DiscordSocketClient _client;
        private readonly string _conn;

        // コンストラクタでClientを受け取り、監視イベントを登録
        public PrskModule(DiscordSocketClient client)
        {
            _client = client;
            _conn = DbConfig.GetConnectionString();

            // 注意: この登録はボット起動時に1回だけ行われる必要があります
            // InteractionServiceがモジュールを読み込む際に呼ばれます
            _client.MessageReceived += OnMessageReceived;
        }

        // --- 1. 設定コマンド ---
        [SlashCommand("prsk_roomid", "プロセカ部屋番号監視を設定")]
        public async Task SetPrsk(
            [Summary("monitor", "番号を書くチャンネル")] ITextChannel monitor, 
            [Summary("target", "名前を変えるチャンネル(VC可)")] IGuildChannel target, 
            [Summary("template", "形式: 部屋【roomid】")] string template)
        {
            using var conn = new NpgsqlConnection(_conn);
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand(@"
                INSERT INTO ""PrskSettings"" (""MonitorChannelId"", ""TargetChannelId"", ""Template"") 
                VALUES (@mc, @tc, @tp) 
                ON CONFLICT (""MonitorChannelId"") DO UPDATE SET ""TargetChannelId"" = EXCLUDED.""TargetChannelId"", ""Template"" = EXCLUDED.""Template""", conn);
            
            cmd.Parameters.AddWithValue("mc", monitor.Id.ToString());
            cmd.Parameters.AddWithValue("tc", target.Id.ToString());
            cmd.Parameters.AddWithValue("tp", template);
            await cmd.ExecuteNonQueryAsync();

            await RespondAsync($"✅ <#{monitor.Id}> での監視を開始しました。\n🐾 番号検知でリアクションとリネームを行います。", ephemeral: true);
        }

        // --- 2. 監視ロジック (理想の動き) ---
        private async Task OnMessageReceived(SocketMessage msg)
        {
            if (msg.Author.IsBot) return;

            // 5桁または6桁の数字を抽出
            var match = Regex.Match(msg.Content, @"\b(\d{5,6})\b");
            if (!match.Success) return;

            var roomId = match.Groups[1].Value;

            try
            {
                using var conn = new NpgsqlConnection(_conn);
                await conn.OpenAsync();

                using var cmd = new NpgsqlCommand(
                    "SELECT \"TargetChannelId\", \"Template\" FROM \"PrskSettings\" WHERE \"MonitorChannelId\" = @mc", conn);
                cmd.Parameters.AddWithValue("mc", msg.Channel.Id.ToString());

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var targetIdStr = reader.GetString(0);
                    var template = reader.GetString(1);

                    // チャンネル名をリネーム
                    if (ulong.TryParse(targetIdStr, out var targetId))
                    {
                        var targetChannel = await _client.GetChannelAsync(targetId) as IGuildChannel;
                        if (targetChannel != null)
                        {
                            string newName = template.Replace("【roomid】", roomId);
                            await targetChannel.ModifyAsync(x => x.Name = newName);
                        }
                    }

                    // メッセージに 🐾 を付ける
                    await msg.AddReactionAsync(new Emoji("🐾"));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Prsk Module Error]: {ex.Message}");
            }
        }
    }
}

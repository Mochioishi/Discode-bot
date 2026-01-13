using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Discord_bot.Infrastructure;
using Dapper;
using System.Text.RegularExpressions;
using System.Text;

namespace Discord_bot.Module
{
    public class PrskModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly DiscordSocketClient _client;
        private readonly DbConfig _db;

        public PrskModule(DiscordSocketClient client, DbConfig db)
        {
            _client = client;
            _db = db;
        }

        [SlashCommand("prsk_roomid", "プロセカのルームID監視を設定します")]
        public async Task SetPrsk(
            [Summary("monitor", "数字を監視するテキストチャンネル")] ITextChannel monitor,
            [Summary("target", "名前を変更する対象のボイス/カテゴリチャンネル")] IGuildChannel target,
            [Summary("template", "変更後の名前形式 (例: 【roomid】協力ライブ)")] string template)
        {
            using var conn = _db.GetConnection();
            const string sql = @"
                INSERT INTO PrskSettings (MonitorChannelId, TargetChannelId, Template, GuildId) 
                VALUES (@mc, @tc, @tp, @gid) 
                ON DUPLICATE KEY UPDATE TargetChannelId = @tc, Template = @tp";

            await conn.ExecuteAsync(sql, new { 
                mc = monitor.Id, 
                tc = target.Id, 
                tp = template, 
                gid = Context.Guild.Id 
            });

            await RespondAsync($"✅ 監視設定を完了しました。\n監視: {monitor.Mention}\n対象: {target.Name}\n形式: {template}", ephemeral: true);
        }

        [SlashCommand("prsk_roomid_list", "プロセカ監視設定の一覧表示")]
        public async Task ListPrsk()
        {
            using var conn = _db.GetConnection();
            const string sql = "SELECT * FROM PrskSettings WHERE GuildId = @gid";
            var settings = (await conn.QueryAsync(sql, new { gid = Context.Guild.Id })).ToList();

            if (!settings.Any())
            {
                await RespondAsync("登録されている監視設定はありません。", ephemeral: true);
                return;
            }

            var sb = new StringBuilder().AppendLine("【プロセカ監視一覧】");
            var builder = new ComponentBuilder();

            foreach (var s in settings)
            {
                var mCh = await _client.GetChannelAsync((ulong)s.MonitorChannelId) as ITextChannel;
                var tCh = await _client.GetChannelAsync((ulong)s.TargetChannelId) as IGuildChannel;
                
                sb.AppendLine($"監視: {mCh?.Name ?? "不明"} -> 対象: {tCh?.Name ?? "不明"}");
                builder.WithButton($"削除: {mCh?.Name ?? "ID:"+s.MonitorChannelId}", $"prsk_del_{s.MonitorChannelId}", ButtonStyle.Danger);
            }

            await RespondAsync(sb.ToString(), components: builder.Build(), ephemeral: true);
        }

        // ボタンによる削除処理
        [ComponentInteraction("prsk_del_*")]
        public async Task DeletePrsk(string monitorId)
        {
            using var conn = _db.GetConnection();
            await conn.ExecuteAsync("DELETE FROM PrskSettings WHERE MonitorChannelId = @id", new { id = ulong.Parse(monitorId) });
            await RespondAsync("✅ 監視設定を削除しました。", ephemeral: true);
        }

        // メッセージ受信時の処理ロジック (Program.cs 等から呼び出すか、別Serviceで管理を推奨)
        // ここでは、設計図に合わせて正規表現とリネームのロジックのみ整理して記述します
        public static async Task HandleMessageAsync(SocketMessage msg, DbConfig db, DiscordSocketClient client)
        {
            if (msg.Author.IsBot) return;

            // 5桁または6桁の数字を抽出
            var match = Regex.Match(msg.Content, @"\b(\d{5,6})\b");
            if (!match.Success) return;

            using var conn = db.GetConnection();
            var setting = await conn.QueryFirstOrDefaultAsync("SELECT TargetChannelId, Template FROM PrskSettings WHERE MonitorChannelId = @mc", new { mc = msg.Channel.Id });

            if (setting != null)
            {
                var targetCh = await client.GetChannelAsync((ulong)setting.TargetChannelId) as IGuildChannel;
                if (targetCh != null)
                {
                    string newName = ((string)setting.Template).Replace("【roomid】", match.Groups[1].Value);
                    await targetCh.ModifyAsync(x => x.Name = newName);
                }
                // リアクション付与 (設計図の🐾)
                await msg.AddReactionAsync(new Emoji("🐾"));
            }
        }
    }
}

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
            [Summary("target", "名前を変更する対象のチャンネル")] IGuildChannel target,
            [Summary("template", "形式 (例: 【roomid】協力ライブ)")] string template)
        {
            // 応答なしエラーを回避
            await DeferAsync(ephemeral: true);

            try
            {
                using var conn = _db.GetConnection();
                // MySQLの ON DUPLICATE KEY ではなく PostgreSQLの ON CONFLICT を使用
                const string sql = @"
                    INSERT INTO PrskSettings (MonitorChannelId, TargetChannelId, Template, GuildId) 
                    VALUES (@mc, @tc, @tp, @gid) 
                    ON CONFLICT (MonitorChannelId) 
                    DO UPDATE SET TargetChannelId = @tc, Template = @tp";

                await conn.ExecuteAsync(sql, new { 
                    mc = (long)monitor.Id, 
                    tc = (long)target.Id, 
                    tp = template, 
                    gid = (long)Context.Guild.Id 
                });

                await FollowupAsync($"✅ 監視設定を完了しました。\n監視: {monitor.Mention}\n対象: {target.Name}\n形式: {template}", ephemeral: true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Prsk Error] {ex.Message}");
                await FollowupAsync("❌ 保存中にエラーが発生しました。DB設定を確認してください。", ephemeral: true);
            }
        }

        [SlashCommand("prsk_roomid_list", "プロセカ監視設定の一覧表示")]
        public async Task ListPrsk()
        {
            await DeferAsync(ephemeral: true);

            using var conn = _db.GetConnection();
            const string sql = "SELECT * FROM PrskSettings WHERE GuildId = @gid";
            var settings = (await conn.QueryAsync(sql, new { gid = (long)Context.Guild.Id })).ToList();

            if (!settings.Any())
            {
                await FollowupAsync("登録されている監視設定はありません。", ephemeral: true);
                return;
            }

            var sb = new StringBuilder().AppendLine("【プロセカ監視一覧】");
            var builder = new ComponentBuilder();

            foreach (var s in settings)
            {
                // PostgreSQLのBIGINTをulongに変換してチャンネル取得
                var mChId = (ulong)(long)s.monitorchannelid; 
                var tChId = (ulong)(long)s.targetchannelid;

                var mCh = await _client.GetChannelAsync(mChId) as ITextChannel;
                var tCh = await _client.GetChannelAsync(tChId) as IGuildChannel;
                
                sb.AppendLine($"監視: {mCh?.Name ?? "不明"} -> 対象: {tCh?.Name ?? "不明"}");
                builder.WithButton($"削除: {mCh?.Name ?? "ID:"+mChId}", $"prsk_del_{mChId}", ButtonStyle.Danger);
            }

            await FollowupAsync(sb.ToString(), components: builder.Build(), ephemeral: true);
        }

        [ComponentInteraction("prsk_del_*")]
        public async Task DeletePrsk(string monitorId)
        {
            await DeferAsync(ephemeral: true);
            using var conn = _db.GetConnection();
            await conn.ExecuteAsync("DELETE FROM PrskSettings WHERE MonitorChannelId = @id", new { id = long.Parse(monitorId) });
            await FollowupAsync("✅ 監視設定を削除しました。", ephemeral: true);
        }

        // メッセージ受信ロジック
        public static async Task HandleMessageAsync(SocketMessage msg, DbConfig db, DiscordSocketClient client)
        {
            if (msg.Author.IsBot) return;

            var match = Regex.Match(msg.Content, @"\b(\d{5,6})\b");
            if (!match.Success) return;

            try
            {
                using var conn = db.GetConnection();
                var setting = await conn.QueryFirstOrDefaultAsync(
                    "SELECT targetchannelid, template FROM PrskSettings WHERE MonitorChannelId = @mc", 
                    new { mc = (long)msg.Channel.Id });

                if (setting != null)
                {
                    var targetChId = (ulong)(long)setting.targetchannelid;
                    var targetCh = await client.GetChannelAsync(targetChId) as IGuildChannel;
                    if (targetCh != null)
                    {
                        string template = setting.template;
                        string newName = template.Replace("【roomid】", match.Groups[1].Value);
                        await targetCh.ModifyAsync(x => x.Name = newName);
                    }
                    await msg.AddReactionAsync(new Emoji("🐾"));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Prsk Msg Error] {ex.Message}");
            }
        }
    }
}

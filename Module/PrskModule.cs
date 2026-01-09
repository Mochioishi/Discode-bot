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

        public PrskModule(DiscordSocketClient client)
        {
            _client = client;
            _conn = DbConfig.GetConnectionString();
            _client.MessageReceived += OnMessageReceived;
        }

        [SlashCommand("prsk_roomid", "プロセカ監視設定")]
        public async Task SetPrsk(ITextChannel monitor, IGuildChannel target, string template)
        {
            using var conn = new NpgsqlConnection(_conn); await conn.OpenAsync();
            using var cmd = new NpgsqlCommand(@"INSERT INTO ""PrskSettings"" (""MonitorChannelId"", ""TargetChannelId"", ""Template"") VALUES (@mc, @tc, @tp) ON CONFLICT (""MonitorChannelId"") DO UPDATE SET ""TargetChannelId"" = EXCLUDED.""TargetChannelId"", ""Template"" = EXCLUDED.""Template""", conn);
            cmd.Parameters.AddWithValue("mc", monitor.Id.ToString());
            cmd.Parameters.AddWithValue("tc", target.Id.ToString());
            cmd.Parameters.AddWithValue("tp", template);
            await cmd.ExecuteNonQueryAsync();
            await RespondAsync("✅ 監視開始", ephemeral: true);
        }

        private async Task OnMessageReceived(SocketMessage msg)
        {
            if (msg.Author.IsBot) return;
            var match = Regex.Match(msg.Content, @"\b(\d{5,6})\b");
            if (!match.Success) return;

            using var conn = new NpgsqlConnection(_conn); await conn.OpenAsync();
            using var cmd = new NpgsqlCommand(@"SELECT ""TargetChannelId"", ""Template"" FROM ""PrskSettings"" WHERE ""MonitorChannelId"" = @mc", conn);
            cmd.Parameters.AddWithValue("mc", msg.Channel.Id.ToString());
            using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync())
            {
                var target = await _client.GetChannelAsync(ulong.Parse(r.GetString(0))) as IGuildChannel;
                if (target != null) await target.ModifyAsync(x => x.Name = r.GetString(1).Replace("【roomid】", match.Groups[1].Value));
                await msg.AddReactionAsync(new Emoji("🐾"));
            }
        }
    }
}

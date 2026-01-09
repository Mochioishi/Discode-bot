using Discord;
using Discord.Interactions;
using DiscordBot.Infrastructure;
using Npgsql;
using System.Threading.Tasks;

namespace DiscordBot.Modules
{
    public class PrskModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly string _conn;
        public PrskModule() => _conn = DbConfig.GetConnectionString();

        [SlashCommand("prsk_roomid", "プロセカ監視設定")]
        public async Task SetPrsk(ITextChannel monitor, IGuildChannel target, string template)
        {
            using var conn = new NpgsqlConnection(_conn);
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand("INSERT INTO \"PrskSettings\" (\"MonitorChannelId\", \"TargetChannelId\", \"Template\") VALUES (@mc, @tc, @tp) ON CONFLICT (\"MonitorChannelId\") DO UPDATE SET \"TargetChannelId\" = EXCLUDED.\"TargetChannelId\", \"Template\" = EXCLUDED.\"Template\"", conn);
            cmd.Parameters.AddWithValue("mc", monitor.Id.ToString());
            cmd.Parameters.AddWithValue("tc", target.Id.ToString());
            cmd.Parameters.AddWithValue("tp", template);
            await cmd.ExecuteNonQueryAsync();
            await RespondAsync("✅ 監視を開始しました", ephemeral: true);
        }
    }
}

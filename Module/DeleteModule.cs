using Discord;
using Discord.Interactions;
using DiscordBot.Infrastructure;
using Npgsql;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DiscordBot.Modules
{
    public class DeleteModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly string _conn;
        private static readonly Dictionary<ulong, ulong> _starts = new();
        public DeleteModule() => _conn = DbConfig.GetConnectionString();

        [SlashCommand("deleteago", "自動掃除設定")]
        public async Task SetPurge(int days, string prot = "None")
        {
            using var conn = new NpgsqlConnection(_conn);
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand("INSERT INTO \"AutoPurgeSettings\" (\"ChannelId\", \"DaysAgo\", \"ProtectionType\") VALUES (@cid, @d, @p) ON CONFLICT (\"ChannelId\") DO UPDATE SET \"DaysAgo\" = EXCLUDED.\"DaysAgo\", \"ProtectionType\" = EXCLUDED.\"ProtectionType\"", conn);
            cmd.Parameters.AddWithValue("cid", Context.Channel.Id.ToString());
            cmd.Parameters.AddWithValue("d", days);
            cmd.Parameters.AddWithValue("p", prot);
            await cmd.ExecuteNonQueryAsync();
            await RespondAsync($"✅ {days}日経過後に掃除します (保護: {prot})", ephemeral: true);
        }

        [MessageCommand("開始地点に設定")]
        public async Task SetStart(IMessage msg) { _starts[Context.User.Id] = msg.Id; await RespondAsync("📍 開始地点を記憶しました", ephemeral: true); }

        [MessageCommand("ここで範囲削除")]
        public async Task DelRange(IMessage msg)
        {
            if (!_starts.TryGetValue(Context.User.Id, out var sId)) { await RespondAsync("❌ 開始地点未設定", ephemeral: true); return; }
            var menu = new SelectMenuBuilder().WithCustomId($"range_exec:{sId}:{msg.Id}").WithPlaceholder("保護設定").AddOption("なし", "None").AddOption("画像", "Image");
            await RespondAsync("実行しますか？", components: new ComponentBuilder().WithSelectMenu(menu).Build(), ephemeral: true);
        }
    }
}

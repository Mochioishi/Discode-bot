using Discord;
using Discord.Interactions;
using DiscordBot.Infrastructure;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DiscordBot.Modules
{
    public class DeleteModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly string _conn;
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<ulong, ulong> _starts = new();

        public DeleteModule() => _conn = DbConfig.GetConnectionString();

        [SlashCommand("delete", "指定した件数を削除")]
        public async Task DeleteMessages(int amount)
        {
            if (amount < 1 || amount > 100) return;
            await DeferAsync(ephemeral: true);
            var msgs = await Context.Channel.GetMessagesAsync(amount).FlattenAsync();
            if (Context.Channel is ITextChannel ch) await ch.DeleteMessagesAsync(msgs);
            await FollowupAsync($"🗑️ {msgs.Count()}件削除しました。", ephemeral: true);
        }

        [MessageCommand("削除")]
        public async Task DeleteSingle(IMessage msg) { await msg.DeleteAsync(); await RespondAsync("🗑️ 削除しました。", ephemeral: true); }

        [SlashCommand("deleteago", "自動掃除設定")]
        public async Task SetPurge(int days, string protection = "None")
        {
            using var conn = new NpgsqlConnection(_conn); await conn.OpenAsync();
            using var cmd = new NpgsqlCommand(@"INSERT INTO ""AutoPurgeSettings"" (""ChannelId"", ""DaysAgo"", ""ProtectionType"") VALUES (@cid, @d, @p) ON CONFLICT (""ChannelId"") DO UPDATE SET ""DaysAgo"" = EXCLUDED.""DaysAgo"", ""ProtectionType"" = EXCLUDED.""ProtectionType""", conn);
            cmd.Parameters.AddWithValue("cid", Context.Channel.Id.ToString());
            cmd.Parameters.AddWithValue("d", days); cmd.Parameters.AddWithValue("p", protection);
            await cmd.ExecuteNonQueryAsync();
            await RespondAsync("✅ 設定完了", ephemeral: true);
        }

        [MessageCommand("開始地点に設定")]
        public async Task SetStart(IMessage msg) { _starts[Context.User.Id] = msg.Id; await RespondAsync("📍 開始地点を記憶しました", ephemeral: true); }

        [MessageCommand("ここで範囲削除")]
        public async Task RangeMenu(IMessage msg)
        {
            if (!_starts.TryGetValue(Context.User.Id, out var sId)) { await RespondAsync("❌ 未設定", ephemeral: true); return; }
            var menu = new SelectMenuBuilder().WithCustomId($"range_exec:{sId}:{msg.Id}").WithPlaceholder("保護ルール").AddOption("なし", "None").AddOption("画像", "Image").AddOption("リンク", "Link");
            await RespondAsync("実行しますか？", components: new ComponentBuilder().WithSelectMenu(menu).Build(), ephemeral: true);
        }

        [ComponentInteraction("range_exec:*:*")]
        public async Task ExecuteRange(string s, string e, string[] choices)
        {
            await DeferAsync(ephemeral: true);
            ulong min = Math.Min(ulong.Parse(s), ulong.Parse(e));
            ulong max = Math.Max(ulong.Parse(s), ulong.Parse(e));
            var msgs = (await Context.Channel.GetMessagesAsync(min, Direction.After, 100).FlattenAsync()).Where(m => m.Id <= max).ToList();
            var startMsg = await Context.Channel.GetMessageAsync(min); if (startMsg != null) msgs.Add(startMsg);

            var toDel = msgs.Where(m => choices[0] switch { "Image" => !m.Attachments.Any(), "Link" => !m.Content.Contains("http"), _ => true }).ToList();
            if (Context.Channel is ITextChannel ch) await ch.DeleteMessagesAsync(toDel);
            await FollowupAsync($"🗑️ {toDel.Count}件削除しました。", ephemeral: true);
        }
    }
}

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

        // --- 1. 即時削除 (Slash Command) ---
        [SlashCommand("delete", "指定した件数のメッセージを削除します")]
        public async Task DeleteMessages([Summary("amount", "1~100件")] int amount)
        {
            if (amount < 1 || amount > 100) { await RespondAsync("1〜100の間で指定してください。", ephemeral: true); return; }
            await DeferAsync(ephemeral: true);
            var msgs = await Context.Channel.GetMessagesAsync(amount).FlattenAsync();
            if (Context.Channel is ITextChannel channel) await channel.DeleteMessagesAsync(msgs);
            await FollowupAsync($"🗑️ {msgs.Count()}件のメッセージを削除しました。", ephemeral: true);
        }

        // --- 2. 1件削除 (Message Command) ---
        [MessageCommand("削除")]
        public async Task DeleteSingleMessage(IMessage msg)
        {
            await msg.DeleteAsync();
            await RespondAsync("🗑️ メッセージを削除しました。", ephemeral: true);
        }

        // --- 3. 自動削除設定 (deleteago) ---
        [SlashCommand("deleteago", "チャンネルの自動掃除を設定します")]
        public async Task SetAutoPurge(int days, string protection = "None")
        {
            using var conn = new NpgsqlConnection(_conn);
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand(@"
                INSERT INTO ""AutoPurgeSettings"" (""ChannelId"", ""DaysAgo"", ""ProtectionType"")
                VALUES (@cid, @d, @p) ON CONFLICT (""ChannelId"") 
                DO UPDATE SET ""DaysAgo"" = EXCLUDED.""DaysAgo"", ""ProtectionType"" = EXCLUDED.""ProtectionType""", conn);
            cmd.Parameters.AddWithValue("cid", Context.Channel.Id.ToString());
            cmd.Parameters.AddWithValue("d", days);
            cmd.Parameters.AddWithValue("p", protection);
            await cmd.ExecuteNonQueryAsync();
            await RespondAsync($"✅ {days}日経過後の自動削除を設定しました。", ephemeral: true);
        }

        // --- 4. 範囲削除 (Message Commands) ---
        [MessageCommand("開始地点に設定")]
        public async Task SetStart(IMessage msg)
        {
            _starts[Context.User.Id] = msg.Id;
            await RespondAsync("📍 開始地点を記憶しました。終了地点で「ここで範囲削除」を選んでください。", ephemeral: true);
        }

        [MessageCommand("ここで範囲削除")]
        public async Task RangeMenu(IMessage msg)
        {
            if (!_starts.TryGetValue(Context.User.Id, out var sId))
            {
                await RespondAsync("❌ 開始地点が設定されていません。", ephemeral: true);
                return;
            }

            var menu = new SelectMenuBuilder()
                .WithCustomId($"range_exec:{sId}:{msg.Id}")
                .WithPlaceholder("保護するルールを選択")
                .AddOption("なし (すべて削除)", "None")
                .AddOption("画像付きを保護", "Image")
                .AddOption("リンク付きを保護", "Link")
                .AddOption("リアクション付きを保護", "Reaction");

            await RespondAsync("削除を実行します。ルールを選んでください：", 
                components: new ComponentBuilder().WithSelectMenu(menu).Build(), ephemeral: true);
        }

        // セレクトメニューの受信 (InteractionHandlerで処理、またはここに記述)
        [ComponentInteraction("range_exec:*:*")]
        public async Task ExecuteRange(string startStr, string endStr, string[] choices)
        {
            await DeferAsync(ephemeral: true);
            ulong startId = ulong.Parse(startStr);
            ulong endId = ulong.Parse(endStr);
            string prot = choices[0];

            var min = Math.Min(startId, endId);
            var max = Math.Max(startId, endId);

            // メッセージ取得とフィルタリング
            var rawMsgs = await Context.Channel.GetMessagesAsync(min, Direction.After, 100).FlattenAsync();
            var targets = rawMsgs.Where(m => m.Id <= max).ToList();
            var startMsg = await Context.Channel.GetMessageAsync(min);
            if (startMsg != null) targets.Add(startMsg);

            var toDelete = targets.Where(m => {
                if (prot == "None") return true;
                if (prot == "Image" && m.Attachments.Any()) return false;
                if (prot == "Link" && (m.Content.Contains("http") || m.Embeds.Any())) return false;
                if (prot == "Reaction" && m.Reactions.Any()) return false;
                return true;
            }).ToList();

            if (Context.Channel is ITextChannel ch) await ch.DeleteMessagesAsync(toDelete);
            _starts.TryRemove(Context.User.Id, out _);
            await FollowupAsync($"🗑️ {toDelete.Count}件削除しました。", ephemeral: true);
        }
    }
}

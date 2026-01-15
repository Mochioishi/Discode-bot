using Discord;
using Discord.Interactions;
using Discord_bot.Infrastructure;
using Dapper;
using System.Collections.Concurrent;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Discord_bot.Module
{
    public class DeleteModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly DbConfig _db;
        private static readonly ConcurrentDictionary<ulong, ulong> _deleteStarts = new();

        public DeleteModule(DbConfig db) => _db = db;

        // --- 1. 自動削除設定 ---

        [SlashCommand("deleteago", "X日経過したメッセージを午前4時に自動削除する設定")]
        public async Task SetDeleteAgo(
            [Summary("days", "何日前までのメッセージを残すか（数値入力）")] int days,
            [Summary("protect", "削除から保護する対象")]
            [Choice("なし", 0), Choice("画像あり", 1), Choice("リアクションあり", 2), Choice("画像またはリアクションあり", 3)] int protect = 0)
        {
            await SaveConfig(Context.Channel.Id, days, protect);
        }

        [SlashCommand("deleteago_list", "自動削除設定の一覧表示")]
        public async Task DeleteAgoList()
        {
            await DeferAsync(ephemeral: true);
            using var conn = _db.GetConnection();
            const string sql = "SELECT * FROM DeleteConfigs WHERE GuildId = @gid";
            var configs = (await conn.QueryAsync(sql, new { gid = (long)Context.Guild.Id })).ToList();

            if (!configs.Any())
            {
                await FollowupAsync("自動削除が設定されているチャンネルはありません", ephemeral: true);
                return;
            }

            var embed = new EmbedBuilder().WithTitle("🗑️ 自動削除設定一覧").WithColor(Color.Red);
            var builder = new ComponentBuilder();

            foreach (var c in configs)
            {
                var channelId = (ulong)(long)c.channelid;
                var days = (int)c.days;
                var protectType = (int)c.protecttype;
                var channel = Context.Guild.GetChannel(channelId);
                string channelName = channel?.Name ?? $"ID:{channelId}";
                string pText = protectType switch { 1 => "画像", 2 => "リアクション", 3 => "画像/リアクション", _ => "なし" };

                embed.AddField($"#{channelName}", $"{days}日前を削除 / 保護: {pText}");
                
                // 「編集」ボタンと「削除」ボタンを並べる
                builder.WithButton("編集", $"delago_edit_{channelId}", ButtonStyle.Primary);
                builder.WithButton("解除", $"delago_rmv_{channelId}", ButtonStyle.Danger);
            }

            await FollowupAsync(embed: embed.Build(), components: builder.Build(), ephemeral: true);
        }

        // 編集ボタンが押された時にモーダルを表示
        [ComponentInteraction("delago_edit_*")]
        public async Task ShowEditModal(string channelId)
        {
            var modal = new ModalBuilder()
                .WithTitle("自動削除設定の編集")
                .WithCustomId($"delago_modal_{channelId}")
                .AddTextInput("残す日数 (数値のみ)", "days_input", placeholder: "例: 7", minLength: 1, maxLength: 3, required: true)
                .AddTextInput("保護設定 (0:なし, 1:画像, 2:リアクション, 3:両方)", "protect_input", placeholder: "0～3の数値を入力", minLength: 1, maxLength: 1, required: true);

            await RespondWithModalAsync(modal.Build());
        }

        // モーダルの送信を受け取る処理
        [ModalInteraction("delago_modal_*")]
        public async Task HandleEditModal(string channelId, DeleteModalData data)
        {
            await DeferAsync(ephemeral: true);
            if (int.TryParse(data.Days, out int days) && int.TryParse(data.Protect, out int protect))
            {
                await SaveConfig(ulong.Parse(channelId), days, Math.Clamp(protect, 0, 3));
            }
            else
            {
                await FollowupAsync("❌ 数値を正しく入力してください。", ephemeral: true);
            }
        }

        // 保存ロジックの共通化
        private async Task SaveConfig(ulong cid, int days, int protect)
        {
            if (!Context.Interaction.HasResponded) await DeferAsync(ephemeral: true);
            
            try
            {
                using var conn = _db.GetConnection();
                const string sql = @"
                    INSERT INTO DeleteConfigs (ChannelId, GuildId, Days, ProtectType) 
                    VALUES (@cid, @gid, @d, @p) 
                    ON CONFLICT (ChannelId) 
                    DO UPDATE SET Days = @d, ProtectType = @p";

                await conn.ExecuteAsync(sql, new { cid = (long)cid, gid = (long)Context.Guild.Id, d = days, p = protect });
                await FollowupAsync("✅ 設定を保存しました。", ephemeral: true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DeleteMode Save Error] {ex}");
                await FollowupAsync("❌ 保存エラーが発生しました。", ephemeral: true);
            }
        }

        // モーダルデータ用クラス
        public class DeleteModalData : IModal
        {
            public string Title => "自動削除設定の編集";
            [InputLabel("残す日数")]
            [ModalTextInput("days_input")]
            public string Days { get; set; }

            [InputLabel("保護(0:なし, 1:画像, 2:リアクション, 3:両方)")]
            [ModalTextInput("protect_input")]
            public string Protect { get; set; }
        }

        // --- 2. 右クリック範囲削除 ---
        // (以前のコードと同様のため省略。ここには以前の🚩コマンド群をそのまま残してください)
        
        [ComponentInteraction("delago_rmv_*")]
        public async Task RemoveDeleteAgo(string channelId)
        {
            await DeferAsync(ephemeral: true);
            using var conn = _db.GetConnection();
            await conn.ExecuteAsync("DELETE FROM DeleteConfigs WHERE ChannelId = @cid", new { cid = long.Parse(channelId) });
            await FollowupAsync("✅ 設定を解除しました", ephemeral: true);
        }
    }
}

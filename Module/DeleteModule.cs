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
        // ユーザーごとの開始地点を保持
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<ulong, ulong> _starts = new();

        public DeleteModule() => _conn = DbConfig.GetConnectionString();

        // 指定件数削除
        [SlashCommand("delete", "指定件数のメッセージを削除")]
        public async Task DeleteMessages(int amount)
        {
            if (amount < 1 || amount > 100) return;
            await DeferAsync(ephemeral: true);
            var msgs = await Context.Channel.GetMessagesAsync(amount).FlattenAsync();
            if (Context.Channel is ITextChannel ch) await ch.DeleteMessagesAsync(msgs);
            await FollowupAsync($"🗑️ {msgs.Count()}件削除しました。", ephemeral: true);
        }

        // メッセージコマンド（右クリック削除）
        [MessageCommand("削除")]
        public async Task DeleteSingle(IMessage msg) 
        { 
            await msg.DeleteAsync(); 
            await RespondAsync("🗑️ 削除しました。", ephemeral: true); 
        }

        // 範囲削除：開始地点
        [MessageCommand("開始地点に設定")]
        public async Task SetStart(IMessage msg) 
        { 
            _starts[Context.User.Id] = msg.Id; 
            await RespondAsync("📍 開始地点を記憶しました。終了地点で「ここで範囲削除」を選んでください。", ephemeral: true); 
        }

        // 範囲削除：実行
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
                .WithPlaceholder("保護ルールを選択")
                .AddOption("なし", "None").AddOption("画像", "Image").AddOption("リンク", "Link");
            
            await RespondAsync("削除を実行しますか？", components: new ComponentBuilder().WithSelectMenu(menu).Build(), ephemeral: true);
        }
    }
}

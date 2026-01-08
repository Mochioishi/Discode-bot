using Discord;
using Discord.Interactions;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace DiscordBot.Modules
{
    public class RoleModule : InteractionModuleBase<SocketInteractionContext>
    {
        // ユーザーIDと設定したいロールの紐付けを一時保持
        public static readonly ConcurrentDictionary<ulong, IRole> PendingSettings = new();

        [SlashCommand("rolegive", "ロールを選択後、対象のメッセージにリアクションして設定を完了させます")]
        public async Task SetRoleCommand([Summary("role", "付与したいロール")] IRole role)
        {
            // 重要：IDをローカル変数にコピーしておく（非同期処理の中で安全に使うため）
            var userId = Context.User.Id;

            // 実行したユーザーIDと、付与したいロールを紐付けて保存
            PendingSettings[userId] = role;

            await RespondAsync(
                $"【設定モード開始】\n1. 付与したいロール: **{role.Name}**\n" +
                $"2. 設定したいメッセージに、**絵文字でリアクション**してください。\n" +
                $"※ 1分間有効です。ボットが反応すれば設定完了です。", 
                ephemeral: true);

            // 1分後に自動的に辞書から削除（タイムアウト処理）
            _ = Task.Delay(60000).ContinueWith(_ => 
            {
                // ここで userId を使う
                PendingSettings.TryRemove(userId, out _);
            });
        }
    }
}

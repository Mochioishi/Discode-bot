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
            var userId = Context.User.Id;
            PendingSettings[userId] = role;

            await RespondAsync(
                $"【設定モード開始】\n1. 付与したいロール: **{role.Name}**\n" +
                $"2. 設定したいメッセージに、**絵文字でリアクション**してください。\n" +
                $"※ 1分間有効です。ボットが反応すれば設定完了です。", 
                ephemeral: true);

            // 1分後に自動削除
            // エラーを避けるため、引数の型を明示的に指定します
            _ = Task.Delay(60000).ContinueWith((Task t) => 
            {
                // 「out _」ではなく、型を明示して取り出す形にします
                PendingSettings.TryRemove(userId, out IRole? removedRole);
            });
        }
    }
}

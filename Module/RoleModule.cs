using Discord;
using Discord.Interactions;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace DiscordBot.Modules
{
    public class RoleModule : InteractionModuleBase<SocketInteractionContext>
    {
        // 「どのユーザー(ulong)」が「どのロール(IRole)」を設定しようとしているかを一時保持
        // 他のクラスからも参照できるように static で宣言します
        public static readonly ConcurrentDictionary<ulong, IRole> PendingSettings = new();

        [SlashCommand("rolegive", "ロールを選択後、対象のメッセージにリアクションして設定を完了させます")]
        public async Task SetRoleCommand([Summary("role", "付与したいロール")] IRole role)
        {
            // 実行したユーザーIDと、付与したいロールを紐付けて保存
            PendingSettings[Context.User.Id] = role;

            await RespondAsync(
                $"【設定モード開始】\n1. 付与したいロール: **{role.Name}**\n" +
                $"2. 設定したいメッセージに、**絵文字でリアクション**してください。\n" +
                $"※ 1分間有効です。ボットが反応すれば設定完了です。", 
                ephemeral: true);

            // 1分後に自動的に辞書から削除（タイムアウト処理）
            _ = Task.Delay(60000).ContinueWith(_ => PendingSettings.TryRemove(Context.User.Id, out _));
        }
    }
}

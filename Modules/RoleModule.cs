using Discord;
using Discord.Interactions;
using DiscordTimeSignal.Data;
using Discord.Data;

namespace DiscordTimeSignal.Modules;

public class RoleModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly DataService _db;

    public RoleModule(DataService db) => _db = db;

    [SlashCommand("rolegive", "メッセージにリアクションでロールを付与する設定をします")]
    public async Task SetRoleGive(
        [Summary("message_id", "対象のメッセージID")] string messageIdStr,
        [Summary("role", "付与するロール")] IRole role,
        [Summary("emoji", "反応させる絵文字")] string emoji)
    {
        if (!ulong.TryParse(messageIdStr, out ulong messageId))
        {
            await RespondAsync("有効なメッセージIDを入力してください。", ephemeral: true);
            return;
        }

        // DBに保存
        await _db.SaveRoleGiveConfigAsync(messageId, role.Id, emoji);

        // Bot自身がそのメッセージにリアクションをつける
        var message = await Context.Channel.GetMessageAsync(messageId);
        if (message != null)
        {
            await message.AddReactionAsync(new Emoji(emoji));
        }

        await RespondAsync($"設定完了：メッセージ({messageId})に {emoji} で {role.Name} を付与します。", ephemeral: true);
    }

    [SlashCommand("rolegive_list", "ロール付与設定の一覧を表示します")]
    public async Task ListRoleGive()
    {
        var configs = await _db.GetRoleGiveConfigsAsync(Context.Guild.Id);
        if (!configs.Any())
        {
            await RespondAsync("設定されているロール付与はありません。", ephemeral: true);
            return;
        }

        var embed = new EmbedBuilder().WithTitle("🎭 ロール付与設定一覧").WithColor(Color.Purple);
        var components = new ComponentBuilder();

        foreach (var c in configs)
        {
            embed.AddField($"メッセージID: {c.MessageId}", $"ロール: <@&{c.RoleId}> / 絵文字: {c.EmojiName}");
            components.WithButton($"削除", $"delete_role_cfg:{c.MessageId}:{c.EmojiName}", ButtonStyle.Danger);
        }

        await RespondAsync(embed: embed.Build(), components: components.Build(), ephemeral: true);
    }
}

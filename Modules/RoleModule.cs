using Discord;
using Discord.Interactions;
using Discord.Data;

namespace Discord.Modules;

public class RoleModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly DataService _db;

    public RoleModule(DataService db) => _db = db;

    [SlashCommand("add-reaction-role", "リアクションロールを設定します")]
    public async Task AddRole(ulong messageId, IRole role, string emoji)
    {
        // 引数 (messageId, roleId, emoji) に合わせる
        await _db.SaveRoleGiveConfigAsync(messageId, role.Id, emoji);
        await RespondAsync($"メッセージ {messageId} に {emoji} で {role.Name} を付与するように設定しました。");
    }
}

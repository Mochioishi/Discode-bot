using Discord;
using Discord.Interactions;
using Discord.Data;

namespace Discord.Modules;

public class CleanerModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly DataService _db;

    public CleanerModule(DataService db) => _db = db;

    [SlashCommand("deleteago", "一定期間過ぎたメッセージを自動削除する設定")]
    public async Task SetCleanup(ITextChannel channel, int days, string protection = "none")
    {
        // DataServiceの新しい引数 (guild, channel, days, type) に合わせる
        await _db.SaveCleanupSettingAsync(Context.Guild.Id, channel.Id, days, protection);
        await RespondAsync($"{channel.Mention} のメッセージを {days} 日後に自動削除するように設定しました。（保護：{protection}）", ephemeral: true);
    }

    [SlashCommand("clean-status", "現在の自動削除設定を確認します")]
    public async Task Status()
    {
        // List版のメソッドを呼び出し
        var settings = await _db.GetCleanupSettingsListAsync(Context.Guild.Id);
        
        if (!settings.Any())
        {
            await RespondAsync("設定されているチャンネルはありません。");
            return;
        }

        var msg = "現在の設定:\n";
        foreach (var set in settings)
        {
            msg += $"<#{set.ChannelId}>: {set.DaysBefore}日後削除 (保護: {set.ProtectionType})\n";
        }
        await RespondAsync(msg, ephemeral: true);
    }

    [SlashCommand("clean-off", "自動削除設定を解除します")]
    public async Task CleanOff()
    {
        await _db.DeleteCleanupSettingAsync(Context.Guild.Id);
        await RespondAsync("設定を解除しました。", ephemeral: true);
    }
}

using Discord;
using Discord.Interactions;

namespace DiscordTimeSignal.Modules;

public class CleanerModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly DataService _db;

    public CleanerModule(DataService db) => _db = db;

    [SlashCommand("deleteago", "X日経過したメッセージを午前4時に自動削除します")]
    public async Task SetDeleteAgo(
        [Summary("days", "何日前のメッセージを消すか")] int days,
        [Summary("protect", "保護対象")] 
        [Choice("なし", "none"), Choice("画像", "image"), Choice("リアクション", "reaction"), Choice("画像とリアクション", "both")] 
        string protect = "none")
    {
        await _db.SaveCleanupSettingAsync(Context.Channel.Id, days, protect);
        await RespondAsync($"設定完了：{days}日以上前のメッセージを毎日04:00に削除します（保護：{protect}）", ephemeral: true);
    }

    [SlashCommand("deleteago_list", "自動削除設定の一覧を表示します")]
    public async Task ListDeleteAgo()
    {
        var settings = await _db.GetCleanupSettingsAsync(Context.Guild.Id);
        var embed = new EmbedBuilder().WithTitle("🧹 自動削除設定一覧").WithColor(Color.Orange);
        var components = new ComponentBuilder();

        foreach (var s in settings)
        {
            embed.AddField($"チャンネル: {s.ChannelName}", $"{s.DaysBefore}日前を削除 (保護: {s.ProtectionType})");
            components.WithButton($"編集/削除 ({s.ChannelName})", $"edit_cleaner:{s.ChannelId}", ButtonStyle.Secondary);
        }

        await RespondAsync(embed: embed.Build(), components: components.Build(), ephemeral: true);
    }
}

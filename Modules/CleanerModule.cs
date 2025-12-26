using Discord;
using Discord.Interactions;
using DiscordTimeSignal.Data;

namespace DiscordTimeSignal.Modules;

[Group("deleteago", "一定期間過ぎたメッセージを自動削除する設定")]
public class CleanerModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly DataService _data;

    public CleanerModule(DataService data)
    {
        _data = data;
    }

    [SlashCommand("set", "実行したチャンネルでX日経過したメッセージを自動削除")]
    public async Task SetAsync(
        [Summary("days", "何日前より前を削除するか")] int days,
        [Summary("protect", "保護対象")] ProtectMode protect = ProtectMode.None)
    {
        if (days <= 0 || days > 365)
        {
            await RespondAsync("日数は1〜365の間で指定してください。", ephemeral: true);
            return;
        }

        var entry = new DeleteAgoEntry
        {
            Id = 0,
            GuildId = Context.Guild.Id,
            ChannelId = Context.Channel.Id,
            Days = days,
            ProtectMode = protect.ToString().ToLower()
        };

        await _data.AddDeleteAgoAsync(entry);

        await RespondAsync(
            $"このチャンネルで **{days}日以前** のメッセージを午前4時に自動削除します。\n" +
            $"保護対象: `{protect}`",
            ephemeral: true);
    }
}

public enum ProtectMode
{
    None,
    Image,
    Reaction,
    Both
}

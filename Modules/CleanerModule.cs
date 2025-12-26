using Discord;
using Discord.Interactions;
using DiscordTimeSignal.Data;

namespace DiscordTimeSignal.Modules;

public enum ProtectMode
{
    None,
    Image,
    Reaction,
    Both
}

public class CleanerModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly DataService _data;

    public CleanerModule(DataService data)
    {
        _data = data;
    }

    // /deleteago
    [SlashCommand("deleteago", "一定期間過ぎたメッセージを自動削除する設定")]
    public async Task DeleteAgoAsync(
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

    // /deleteago_list
    [SlashCommand("deleteago_list", "deleteagoで登録した内容を一覧表示")]
    public async Task DeleteAgoListAsync()
    {
        // ★ 修正：引数なしで呼ぶ
        var entries = await _data.GetAllDeleteAgoAsync();
        var list = entries
            .Where(e => e.GuildId == Context.Guild.Id && e.ChannelId == Context.Channel.Id)
            .ToList();

        if (list.Count == 0)
        {
            await RespondAsync("このチャンネルには deleteago の設定がありません。", ephemeral: true);
            return;
        }

        var embed = new EmbedBuilder()
            .WithTitle("deleteago 設定一覧")
            .WithColor(Color.Blue);

        foreach (var e in list)
        {
            embed.AddField(
                $"ID: {e.Id}",
                $"日数: **{e.Days}日**\n保護: `{e.ProtectMode}`",
                inline: false);
        }

        await RespondAsync(embed: embed.Build(), ephemeral: true);
    }
}

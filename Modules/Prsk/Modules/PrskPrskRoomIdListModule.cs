using Discord;
using Discord.Interactions;
using DiscordTimeSignal.Data;

namespace DiscordTimeSignal.Modules.Prsk;

[Group("prsk_roomid_list", "prsk_roomid設定の一覧")]
public class PrskRoomIdListModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly DataService _data;

    public PrskRoomIdListModule(DataService data)
    {
        _data = data;
    }

    [SlashCommand("show", "prsk_roomidで登録した内容を一覧表示します")]
    public async Task ShowAsync()
    {
        var entries = await _data.GetPrskRoomIdsAsync(Context.Guild.Id);
        var list = entries.ToList();

        if (list.Count == 0)
        {
            await RespondAsync("このサーバーには prsk_roomid の設定がありません。", ephemeral: true);
            return;
        }

        var embed = new EmbedBuilder()
            .WithTitle("prsk_roomid 設定一覧")
            .WithColor(Color.Purple);

        foreach (var e in list)
        {
            embed.AddField(
                $"ID: {e.Id}",
                $"監視: <#{e.WatchChannelId}>\n対象: <#{e.TargetChannelId}>\nformat: `{e.NameFormat}`",
                inline: false);
        }

        await RespondAsync(embed: embed.Build(), ephemeral: true);
    }
}

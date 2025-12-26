using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordTimeSignal.Data;

namespace DiscordTimeSignal.Modules.Prsk;

[Group("prsk_roomid", "prskのルームID監視・チャンネル名変更")]
public class PrskRoomIdModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly DataService _data;
    private readonly DiscordSocketClient _client;

    public PrskRoomIdModule(DataService data, DiscordSocketClient client)
    {
        _data = data;
        _client = client;

        _client.MessageReceived += OnMessageReceived;
    }

    [SlashCommand("set", "監視チャンネルと対象チャンネルを登録します")]
    public async Task SetAsync(
        [Summary("watch", "ルームIDを監視するチャンネル")] ITextChannel watch,
        [Summary("target", "名前を変更する対象チャンネル")] ITextChannel target,
        [Summary("name_format", "チャンネル名フォーマット（{roomid} が置換される）")]
        string nameFormat = "ex【{roomid}】")
    {
        var entry = new PrskRoomIdEntry
        {
            Id = 0,
            GuildId = Context.Guild.Id,
            WatchChannelId = watch.Id,
            TargetChannelId = target.Id,
            NameFormat = nameFormat
        };

        await _data.AddPrskRoomIdAsync(entry);

        await RespondAsync(
            $"監視チャンネル: {watch.Mention}\n" +
            $"対象チャンネル: {target.Mention}\n" +
            $"フォーマット: `{nameFormat}`\n" +
            $"として登録しました。",
            ephemeral: true);
    }

    private async Task OnMessageReceived(SocketMessage message)
    {
        if (message.Author.IsBot) return;
        if (message.Channel is not SocketTextChannel channel) return;

        var text = message.Content.Trim();

        if (!int.TryParse(text, out var num)) return;
        if (text.Length < 5 || text.Length > 6) return;

        var entries = await _data.GetPrskRoomIdsAsync(channel.Guild.Id);
        var match = entries.FirstOrDefault(e => e.WatchChannelId == channel.Id);
        if (match == null) return;

        var target = channel.Guild.GetTextChannel(match.TargetChannelId);
        if (target == null) return;

        var newName = match.NameFormat.Replace("{roomid}", text);
        await target.ModifyAsync(p => p.Name = newName);

        await message.AddReactionAsync(new Emoji("🐾"));
    }
}

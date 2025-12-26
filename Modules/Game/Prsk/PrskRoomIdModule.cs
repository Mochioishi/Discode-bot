using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordTimeSignal.Data;

namespace DiscordTimeSignal.Modules.Game.Prsk;

//
// prsk_roomid 設定コマンド
//
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

//
// prsk_roomid_list 一覧コマンド
//
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

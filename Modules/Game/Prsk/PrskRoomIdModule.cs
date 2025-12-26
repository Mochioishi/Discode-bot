using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordTimeSignal.Data;

namespace DiscordTimeSignal.Modules.Game.Prsk;

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

    // /prsk_roomid
    [SlashCommand("prsk_roomid", "prskのルームID監視とチャンネル名変更を設定します")]
    public async Task PrskRoomIdAsync(
        [Summary("watch", "ルームIDを監視するテキストチャンネル")] ITextChannel watch,
        [Summary("target", "名前を変更する対象チャンネル（テキストまたはボイス）")] IGuildChannel target,
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
            $"対象チャンネル: <#{target.Id}>\n" +
            $"フォーマット: `{nameFormat}`\n" +
            $"として登録しました。",
            ephemeral: true);
    }

    // /prsk_roomid_list
    [SlashCommand("prsk_roomid_list", "prsk_roomidで登録した内容を一覧表示します")]
    public async Task PrskRoomIdListAsync()
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

    private async Task OnMessageReceived(SocketMessage message)
    {
        if (message.Author.IsBot) return;
        if (message.Channel is not SocketTextChannel channel) return;

        var text = message.Content.Trim();

        // 5〜6桁の数字のみ対象
        if (!int.TryParse(text, out var num)) return;
        if (text.Length < 5 || text.Length > 6) return;

        var entries = await _data.GetPrskRoomIdsAsync(channel.Guild.Id);
        var match = entries.FirstOrDefault(e => e.WatchChannelId == channel.Id);
        if (match == null) return;

        var guild = channel.Guild;
        var targetChannel = guild.GetChannel(match.TargetChannelId);
        if (targetChannel == null) return;

        var newName = match.NameFormat.Replace("{roomid}", text);

        switch (targetChannel)
        {
            case ITextChannel textChannel:
                await textChannel.ModifyAsync(p => p.Name = newName);
                break;
            case IVoiceChannel voiceChannel:
                await voiceChannel.ModifyAsync(p => p.Name = newName);
                break;
            default:
                // テキスト/ボイス以外（カテゴリなど）は無視
                return;
        }

        await message.AddReactionAsync(new Emoji("🐾"));
    }
}

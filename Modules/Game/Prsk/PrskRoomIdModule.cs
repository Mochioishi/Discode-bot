using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordTimeSignal.Data;

namespace DiscordTimeSignal.Modules.Game.Prsk;

public class PrskRoomIdModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly DataService _data;

    public PrskRoomIdModule(DataService data)
    {
        _data = data;
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
            .WithTitle("🎵 prsk_roomid 設定一覧")
            .WithColor(Color.Blue);

        var components = new ComponentBuilder();

        foreach (var e in list)
        {
            embed.AddField(
                $"ID: {e.Id}",
                $"監視: <#{e.WatchChannelId}>\n" +
                $"対象: <#{e.TargetChannelId}>\n" +
                $"format: `{e.NameFormat}`",
                inline: false);

            components.WithButton(
                $"削除 {e.Id}",
                $"delete_prsk_{e.Id}",
                ButtonStyle.Danger
            );
        }

        await RespondAsync(embed: embed.Build(), components: components.Build(), ephemeral: true);
    }

    // 削除ボタン
    [ComponentInteraction("delete_prsk_*")]
    public async Task DeletePrskAsync(string id)
    {
        long entryId = long.Parse(id);
        await _data.DeletePrskRoomIdAsync(entryId);
        await RespondAsync($"ID {entryId} を削除しました。", ephemeral: true);
    }

    // Program.cs で登録される
    public async Task OnMessageReceived(SocketMessage message)
    {
        try
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

            if (targetChannel is ITextChannel textChannel)
                await textChannel.ModifyAsync(p => p.Name = newName);
            else if (targetChannel is IVoiceChannel voiceChannel)
                await voiceChannel.ModifyAsync(p => p.Name = newName);

            await message.AddReactionAsync(new Emoji("🐾"));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PrskRoomId ERROR] {ex}");
        }
    }
}

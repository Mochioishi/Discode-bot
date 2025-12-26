using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace DiscordTimeSignal.Modules;

public class DeleteRangeModule : InteractionModuleBase<SocketInteractionContext>
{
    private static readonly Dictionary<(ulong GuildId, ulong ChannelId, ulong UserId), ulong> RangeStart
        = new();

    // 範囲削除開始
    [MessageCommand("範囲削除開始")]
    public async Task SetStartAsync(IMessage message)
    {
        var key = (Context.Guild.Id, Context.Channel.Id, Context.User.Id);
        RangeStart[key] = message.Id;

        var embed = new EmbedBuilder()
            .WithTitle("🧹 範囲削除開始")
            .WithDescription("開始位置を設定しました。次に **範囲削除終了** を選択してください。")
            .WithColor(Color.Blue)
            .Build();

        await RespondAsync(embed: embed, ephemeral: true);
    }

    // 範囲削除終了
    [MessageCommand("範囲削除終了")]
    public async Task SetEndAsync(IMessage message)
    {
        var key = (Context.Guild.Id, Context.Channel.Id, Context.User.Id);

        if (!RangeStart.TryGetValue(key, out var startId))
        {
            var errorEmbed = new EmbedBuilder()
                .WithTitle("⚠ エラー")
                .WithDescription("開始位置が設定されていません。\n先に **範囲削除開始** を実行してください。")
                .WithColor(Color.Blue)
                .Build();

            await RespondAsync(embed: errorEmbed, ephemeral: true);
            return;
        }

        var start = startId;
        var end = message.Id;

        if (Context.Channel is not ITextChannel textChannel)
        {
            var errorEmbed = new EmbedBuilder()
                .WithTitle("⚠ エラー")
                .WithDescription("テキストチャンネルでのみ動作します。")
                .WithColor(Color.Blue)
                .Build();

            await RespondAsync(embed: errorEmbed, ephemeral: true);
            return;
        }

        var msgs = await textChannel.GetMessagesAsync(limit: 1000).FlattenAsync();
        var range = msgs
            .Where(m => (m.Id >= start && m.Id <= end) || (m.Id >= end && m.Id <= start))
            .OrderBy(m => m.Id)
            .ToList();

        int count = 0;

        foreach (var msg in range)
        {
            try
            {
                await msg.DeleteAsync();
                count++;
            }
            catch
            {
                // 権限不足などは無視
            }
        }

        RangeStart.Remove(key);

        var embed = new EmbedBuilder()
            .WithTitle("🧹 範囲削除完了")
            .WithDescription($"メッセージを **{count} 件** 削除しました。")
            .WithColor(Color.Blue)
            .Build();

        await RespondAsync(embed: embed, ephemeral: true);
    }
}

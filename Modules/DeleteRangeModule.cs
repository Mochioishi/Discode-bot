using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace DiscordTimeSignal.Modules;

public class DeleteRangeModule : InteractionModuleBase<SocketInteractionContext>
{
    private static readonly Dictionary<(ulong GuildId, ulong ChannelId, ulong UserId), ulong> RangeStart
        = new();

    [MessageCommand("範囲削除開始")]
    public async Task SetStartAsync(IMessage message)
    {
        var key = (Context.Guild.Id, Context.Channel.Id, Context.User.Id);
        RangeStart[key] = message.Id;

        await RespondAsync(
            $"開始位置を設定しました。",
            ephemeral: true);
    }

    [MessageCommand("範囲削除終了")]
    public async Task SetEndAsync(IMessage message)
    {
        var key = (Context.Guild.Id, Context.Channel.Id, Context.User.Id);

        if (!RangeStart.TryGetValue(key, out var startId))
        {
            await RespondAsync("開始位置が設定されていません。先に **範囲削除開始** を実行してください。", ephemeral: true);
            return;
        }

        var start = startId;
        var end = message.Id;

        if (Context.Channel is not ITextChannel textChannel)
        {
            await RespondAsync("テキストチャンネルでのみ動作します。", ephemeral: true);
            return;
        }

        var msgs = await textChannel.GetMessagesAsync(limit: 1000).FlattenAsync();
        var range = msgs
            .Where(m => (m.Id >= start && m.Id <= end) || (m.Id >= end && m.Id <= start))
            .OrderBy(m => m.Id)
            .ToList();

        foreach (var msg in range)
        {
            try
            {
                await msg.DeleteAsync();
            }
            catch
            {
                // 権限不足などは無視
            }
        }

        RangeStart.Remove(key);

        // ← これが必要
        var count = range.Count;

        await RespondAsync(
            $"メッセージを **{count} 件** 削除しました。",
            ephemeral: true);
    }
}

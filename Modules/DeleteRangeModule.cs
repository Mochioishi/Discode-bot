using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace DiscordTimeSignal.Modules;

public class DeleteRangeModule : InteractionModuleBase<SocketInteractionContext>
{
    private static readonly Dictionary<(ulong GuildId, ulong ChannelId, ulong UserId), ulong> RangeStart
        = new();

    [MessageCommand("Delete_Range_Start")]
    public async Task SetStartAsync(IMessage message)
    {
        var key = (Context.Guild.Id, Context.Channel.Id, Context.User.Id);
        RangeStart[key] = message.Id;

        await RespondAsync(
            $"開始位置をメッセージID `{message.Id}` に設定しました。\n" +
            $"終了位置を指定するには、削除範囲の最後のメッセージで `Delete_Range_End` を実行してください。",
            ephemeral: true);
    }

    [MessageCommand("Delete_Range_End")]
    public async Task SetEndAsync(IMessage message)
    {
        var key = (Context.Guild.Id, Context.Channel.Id, Context.User.Id);

        if (!RangeStart.TryGetValue(key, out var startId))
        {
            await RespondAsync("開始位置が設定されていません。先に `Delete_Range_Start` を実行してください。", ephemeral: true);
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
                // 権限不足など
            }
        }

        RangeStart.Remove(key);

        await RespondAsync($"メッセージ `{start}` から `{end}` までを削除しました。", ephemeral: true);
    }
}

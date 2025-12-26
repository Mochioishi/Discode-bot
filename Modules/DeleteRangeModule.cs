using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace DiscordTimeSignal.Modules;

public class DeleteRangeModule : InteractionModuleBase<SocketInteractionContext>
{
    private static readonly Dictionary<(ulong GuildId, ulong ChannelId, ulong UserId), ulong> RangeStart = new();
    private static readonly Dictionary<(ulong GuildId, ulong ChannelId, ulong UserId), ulong> RangeEnd = new();

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

        RangeEnd[key] = message.Id;

        var embed = new EmbedBuilder()
            .WithTitle("🧹 範囲削除終了")
            .WithDescription("終了位置を設定しました。\n次に `/deleterange` を実行してください。")
            .WithColor(Color.Blue)
            .Build();

        await RespondAsync(embed: embed, ephemeral: true);
    }

    // /deleterange
    [SlashCommand("deleterange", "範囲削除を実行します（未指定時は保護なし）")]
    public async Task DeleteRangeAsync(
        [Summary("protect", "保護対象（未指定時は保護しない）")]
        ProtectMode protect = ProtectMode.None)
    {
        var key = (Context.Guild.Id, Context.Channel.Id, Context.User.Id);

        if (!RangeStart.TryGetValue(key, out var startId) ||
            !RangeEnd.TryGetValue(key, out var endId))
        {
            var errorEmbed = new EmbedBuilder()
                .WithTitle("⚠ エラー")
                .WithDescription("開始位置または終了位置が設定されていません。\n先に **範囲削除開始** と **範囲削除終了** を実行してください。")
                .WithColor(Color.Blue)
                .Build();

            await RespondAsync(embed: errorEmbed, ephemeral: true);
            return;
        }

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
            .Where(m => (m.Id >= startId && m.Id <= endId) || (m.Id >= endId && m.Id <= startId))
            .OrderBy(m => m.Id)
            .ToList();

        int count = 0;

        foreach (var msg in range)
        {
            // 保護判定
            if (protect == ProtectMode.Image && msg.Attachments.Count > 0)
                continue;

            if (protect == ProtectMode.Reaction && msg.Reactions.Count > 0)
                continue;

            if (protect == ProtectMode.Both &&
                (msg.Attachments.Count > 0 || msg.Reactions.Count > 0))
                continue;

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

        // 範囲情報をクリア
        RangeStart.Remove(key);
        RangeEnd.Remove(key);

        var embed = new EmbedBuilder()
            .WithTitle("🧹 範囲削除完了")
            .WithDescription(
                $"削除件数: **{count} 件**\n" +
                $"保護対象: `{protect}`\n" +
                $"（未指定時は保護なし）")
            .WithColor(Color.Blue)
            .Build();

        await RespondAsync(embed: embed, ephemeral: true);
    }
}

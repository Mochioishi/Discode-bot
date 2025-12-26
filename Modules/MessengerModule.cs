using Discord;
using Discord.Interactions;
using DiscordTimeSignal.Data;

namespace DiscordTimeSignal.Modules;

public class MessengerModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly DataService _data;

    public MessengerModule(DataService data)
    {
        _data = data;
    }

    // /bottext
    [SlashCommand("bottext", "実行したチャンネルでbotを喋らせる")]
    public async Task BotTextAsync(
        [Summary("text", "送信するテキスト")] string text,
        [Summary("embed", "埋め込み形式で送信するか")] bool isEmbed = false,
        [Summary("title", "埋め込みタイトル（省略可）")] string? title = null,
        [Summary("time", "hh:mm形式の時間に予約（省略可）")] string? timeHhmm = null
    )
    {
        // time 未指定 → 即時送信
        if (string.IsNullOrWhiteSpace(timeHhmm))
        {
            if (isEmbed)
            {
                var embed = new EmbedBuilder()
                    .WithTitle(string.IsNullOrWhiteSpace(title) ? null : title)
                    .WithDescription(text)
                    .WithColor(Color.Blue)
                    .Build();

                await Context.Channel.SendMessageAsync(embed: embed);
            }
            else
            {
                await Context.Channel.SendMessageAsync(text);
            }

            await RespondAsync("メッセージを送信しました。", ephemeral: true);
            return;
        }

        // 予約送信
        if (!TimeSpan.TryParse(timeHhmm, out _))
        {
            await RespondAsync("時間は `HH:mm` 形式で指定してください。", ephemeral: true);
            return;
        }

        var entry = new BotTextEntry
        {
            GuildId = Context.Guild.Id,
            ChannelId = Context.Channel.Id,
            Content = text,
            IsEmbed = isEmbed,
            EmbedTitle = title,
            TimeHhmm = timeHhmm
        };

        var id = await _data.AddBotTextAsync(entry);

        await RespondAsync(
            $"ID: `{id}` として予約しました。\n" +
            $"時間: `{timeHhmm}` / 埋め込み: `{isEmbed}`",
            ephemeral: true);
    }

    // /bottext_list
    [SlashCommand("bottext_list", "bottextで登録した内容を一覧にする")]
    public async Task BotTextListAsync()
    {
        // ★ ギルド全体の予約を取得
        var entries = await _data.GetBotTextsByGuildAsync(Context.Guild.Id);
        var list = entries.ToList();

        if (list.Count == 0)
        {
            await RespondAsync("このサーバーには予約メッセージがありません。", ephemeral: true);
            return;
        }

        var embed = new EmbedBuilder()
            .WithTitle("📝 bottext 予約一覧（")
            .WithColor(Color.Blue);

        var components = new ComponentBuilder();

        foreach (var e in list)
        {
            embed.AddField(
                $"ID: {e.Id}",
                $"チャンネル: <#{e.ChannelId}>\n" +
                $"時間: `{e.TimeHhmm}`\n" +
                $"埋め込み: `{e.IsEmbed}`\n" +
                $"内容: {e.Content}",
                inline: false
            );

            components.WithButton(
                $"削除 {e.Id}",
                $"delete_bottext_{e.Id}",
                ButtonStyle.Danger
            );
        }

        await RespondAsync(embed: embed.Build(), components: components.Build(), ephemeral: true);
    }

    // ★ 削除ボタン
    [ComponentInteraction("delete_bottext_*")]
    public async Task DeleteBotTextAsync(string id)
    {
        long entryId = long.Parse(id);
        await _data.DeleteBotTextAsync(entryId);
        await RespondAsync($"ID {entryId} を削除しました。", ephemeral: true);
    }
}

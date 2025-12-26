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
        [Summary("time", "hh:mm形式の時間（省略時は即時送信）")] string? timeHhmm = null,
        [Summary("embed", "埋め込み形式で送信するか")] bool isEmbed = false,
        [Summary("title", "埋め込みタイトル（省略可）")] string? title = null)
    {
        // time 未指定 → 即時送信
        if (string.IsNullOrWhiteSpace(timeHhmm))
        {
            if (isEmbed)
            {
                var embed = new EmbedBuilder()
                    .WithTitle(string.IsNullOrWhiteSpace(title) ? null : title)
                    .WithDescription(text)
                    .WithColor(Color.Orange)
                    .Build();

                await Context.Channel.SendMessageAsync(embed: embed);
            }
            else
            {
                await Context.Channel.SendMessageAsync(text);
            }

            await RespondAsync("メッセージを即時送信しました。", ephemeral: true);
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
            Id = 0,
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
        var entries = await _data.GetBotTextsAsync(Context.Guild.Id, Context.Channel.Id);
        var list = entries.ToList();

        if (list.Count == 0)
        {
            await RespondAsync("このチャンネルには予約メッセージがありません。", ephemeral: true);
            return;
        }

        var embed = new EmbedBuilder()
            .WithTitle("bottext 予約一覧")
            .WithColor(Color.Orange);

        foreach (var e in list)
        {
            embed.AddField(
                $"ID: {e.Id}",
                $"時間: `{e.TimeHhmm}` / 埋め込み: `{e.IsEmbed}`\n内容: {e.Content}",
                inline: false);
        }

        await RespondAsync(embed: embed.Build(), ephemeral: true);
    }
}

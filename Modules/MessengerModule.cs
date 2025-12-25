using Discord;
using Discord.Interactions;
using Discord.Data;

namespace DiscordTimeSignal.Modules;

public class MessengerModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly DataService _db;

    public MessengerModule(DataService db)
    {
        _db = db;
    }

    [SlashCommand("bottext", "実行したチャンネルでbotを喋らせます")]
    public async Task HandleBotText(
        string text, 
        [Summary("time", "時間指定 (hh:mm) ※次にその時刻になった時に送信")] string? time = null,
        [Summary("is_embed", "埋め込み形式にするか")] bool isEmbed = false,
        [Summary("title", "埋め込み時のタイトル")] string? title = null)
    {
        if (string.IsNullOrEmpty(time))
        {
            // 即時送信
            if (isEmbed)
            {
                var embed = new EmbedBuilder().WithTitle(title).WithDescription(text).WithColor(Color.Blue).Build();
                await RespondAsync(embed: embed);
            }
            else
            {
                await RespondAsync(text);
            }
        }
        else
        {
            // 予約登録 (DBへ保存)
            var task = new BotMessageTask
            {
                ChannelId = Context.Channel.Id,
                Content = text,
                IsEmbed = isEmbed,
                EmbedTitle = title,
                ScheduledTime = time
            };
            await _db.SaveMessageTaskAsync(task);
            await RespondAsync($"予約しました: {time} に送信します。", ephemeral: true);
        }
    }

    [SlashCommand("bottext_list", "bottextで登録した内容を一覧表示・削除します")]
    public async Task HandleBotTextList()
    {
        var tasks = await _db.GetMessageTasksByChannelAsync(Context.Channel.Id);
        
        if (!tasks.Any())
        {
            await RespondAsync("登録されているメッセージはありません。", ephemeral: true);
            return;
        }

        var embed = new EmbedBuilder()
            .WithTitle("📢 Botメッセージ予約一覧")
            .WithColor(Color.Green);

        var components = new ComponentBuilder();

        foreach (var task in tasks)
        {
            string info = $"時刻: {task.ScheduledTime ?? "即時"}\n内容: {task.Content.Substring(0, Math.Min(task.Content.Length, 20))}...";
            embed.AddField(task.ScheduledTime ?? "即時", info);
            
            // 削除ボタンを各タスクごとに追加 (IDをカスタムIDに埋め込む)
            components.WithButton($"削除 ({task.ScheduledTime})", $"delete_task:{task.Id}", ButtonStyle.Danger);
        }

        await RespondAsync(embed: embed.Build(), components: components.Build(), ephemeral: true);
    }

    // ボタン操作の処理 (ComponentInteraction)
    [ComponentInteraction("delete_task:*")]
    public async Task DeleteTaskHandler(string taskId)
    {
        await _db.DeleteMessageTaskAsync(Guid.Parse(taskId));
        await RespondAsync("予約を削除しました。", ephemeral: true);
    }
}

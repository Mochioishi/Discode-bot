using Discord;
using Discord.Interactions;
using Discord.Data;

namespace Discord.Modules;

public class MessengerModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly DataService _db;

    public MessengerModule(DataService db) => _db = db;

    [SlashCommand("bottext", "指定した時間にメッセージを予約送信します")]
    public async Task AddTask(ITextChannel channel, string content, string time)
    {
        if (DateTime.TryParse(time, out var scheduledTime))
        {
            await _db.SaveMessageTaskAsync(channel.Id, content, scheduledTime);
            await RespondAsync($"{time} に送信予約を受け付けました。", ephemeral: true);
        }
        else
        {
            await RespondAsync("時間の形式が正しくありません。(例: 2025/12/25 18:00)", ephemeral: true);
        }
    }

    [SlashCommand("text-list", "予約中のメッセージ一覧を表示します")]
    public async Task ListTasks(ITextChannel channel)
    {
        // 型を MessageTask に統一
        IEnumerable<MessageTask> tasks = await _db.GetMessageTasksByChannelAsync(channel.Id);

        if (!tasks.Any())
        {
            await RespondAsync("予約されたメッセージはありません。");
            return;
        }

        var msg = "現在の予約:\n";
        foreach (var t in tasks)
        {
            // DateTime型なのでToStringで表示
            msg += $"[{t.ScheduledTime:HH:mm}] {t.Content}\n";
        }
        await RespondAsync(msg, ephemeral: true);
    }
}

using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordTimeSignal.Data;

namespace DiscordTimeSignal.Modules;

public class CleanerModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly DataService _db;
    // 右クリック削除の開始地点を記録（ユーザーID, メッセージID）
    private static readonly Dictionary<ulong, ulong> _startPoints = new();

    public CleanerModule(DataService db) => _db = db;

    // --- 自動削除設定 (deleteago) ---
    [SlashCommand("deleteago", "X日経過したメッセージを午前4時に自動削除します")]
    public async Task SetDeleteAgo(
        [Summary("days", "何日前のメッセージを消すか")] int days,
        [Summary("protect", "保護対象")] 
        [Choice("なし", "none"), Choice("画像", "image"), Choice("リアクション", "reaction"), Choice("画像とリアクション", "both")] 
        string protect = "none")
    {
        await _db.SaveCleanupSettingAsync(Context.Guild.Id, Context.Channel.Id, days, protect);
        await RespondAsync($"✅ 設定完了：{days}日以上前のメッセージを毎日04:00に削除します（保護：{protect}）", ephemeral: true);
    }

    // --- 自動削除設定の一覧 (deleteago_list) ---
    [SlashCommand("deleteago_list", "自動削除設定の一覧を表示します")]
    public async Task ListDeleteAgo()
    {
        var settings = await _db.GetCleanupSettingsAsync(Context.Guild.Id);
        if (!settings.Any())
        {
            await RespondAsync("設定されているチャンネルはありません。", ephemeral: true);
            return;
        }

        var embed = new EmbedBuilder().WithTitle("🧹 自動削除設定一覧").WithColor(Color.Orange);
        var components = new ComponentBuilder();

        foreach (var s in settings)
        {
            embed.AddField($"チャンネル: {s.ChannelName}", $"{s.DaysBefore}日前を削除 (保護: {s.ProtectionType})");
            // ボタンのカスタムIDにチャンネルIDを埋め込み、後の削除処理で識別できるようにする
            components.WithButton($"削除 ({s.ChannelName})", $"delete_conf:{s.ChannelId}", ButtonStyle.Danger);
        }

        await RespondAsync(embed: embed.Build(), components: components.Build(), ephemeral: true);
    }

    // --- 右クリック削除：開始地点 ---
    [MessageCommand("開始場所として指定")]
    public async Task SetStartPoint(IMessage message)
    {
        _startPoints[Context.User.Id] = message.Id;
        await RespondAsync("📍 **開始地点**を設定しました。\n次に、削除したい最後のメッセージを右クリックして「終了場所（ここまで削除）」を選択してください。", ephemeral: true);
    }

    // --- 右クリック削除：終了地点＆実行 ---
    [MessageCommand("終了場所（ここまで削除）")]
    public async Task SetEndPoint(IMessage endMessage)
    {
        if (!_startPoints.TryGetValue(Context.User.Id, out var startId))
        {
            await RespondAsync("❌ 先に「開始場所として指定」を右クリックで選んでください。", ephemeral: true);
            return;
        }

        await DeferAsync(ephemeral: true); // 処理中の「考え中...」状態

        var messages = await Context.Channel.GetMessagesAsync(startId, Direction.After, 100).FlattenAsync();
        var targets = messages.Where(m => m.Id <= endMessage.Id).ToList();
        
        var startMsg = await Context.Channel.GetMessageAsync(startId);
        if (startMsg != null) targets.Add(startMsg);

        if (Context.Channel is ITextChannel textChannel && targets.Any())
        {
            var twoWeeksAgo = DateTimeOffset.UtcNow.AddDays(-14);
            var bulkDeleteList = targets.Where(m => m.CreatedAt > twoWeeksAgo).ToList();
            var manualDeleteList = targets.Where(m => m.CreatedAt <= twoWeeksAgo).ToList();

            if (bulkDeleteList.Any()) await textChannel.DeleteMessagesAsync(bulkDeleteList);
            foreach (var m in manualDeleteList) await m.DeleteAsync();

            await FollowupAsync($"🗑️ {targets.Count}件のメッセージを範囲削除しました。", ephemeral: true);
        }

        _startPoints.Remove(Context.User.Id);
    }

    // --- 一覧から削除ボタンを押した時の処理 ---
    [ComponentInteraction("delete_conf:*")]
    public async Task DeleteConfigHandler(string channelId)
    {
        await _db.DeleteCleanupSettingAsync(ulong.Parse(channelId));
        await RespondAsync("設定を削除しました。", ephemeral: true);
    }
}

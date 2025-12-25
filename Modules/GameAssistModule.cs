using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordTimeSignal.Data;
using System.Text.RegularExpressions;
using Discord.Data;

namespace DiscordTimeSignal.Modules;

public class GameAssistModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly DataService _db;
    private readonly DiscordSocketClient _client;

    public GameAssistModule(DataService db, DiscordSocketClient client)
    {
        _db = db;
        _client = client;
    }

    // --- 設定コマンド ---
    [SlashCommand("prsk_roomid", "ルームID監視を設定します")]
    public async Task SetPrskRoom(
        ITextChannel monitorChannel, 
        ITextChannel targetChannel, 
        string format = "【roomid】")
    {
        var config = new GameRoomConfig
        {
            GuildId = Context.Guild.Id,
            MonitorChannelId = monitorChannel.Id,
            TargetChannelId = targetChannel.Id,
            OriginalNameFormat = format
        };
        
        await _db.SaveGameRoomConfigAsync(config);
        await RespondAsync($"設定完了！{monitorChannel.Mention} で5-6桁の数字が出たら、{targetChannel.Name} の名前を変更します。", ephemeral: true);
    }

    [SlashCommand("prsk_roomid_list", "ルームID監視設定の一覧を表示します")]
    public async Task ListPrskRoom()
    {
        var configs = await _db.GetGameRoomConfigsAsync(Context.Guild.Id);
        var embed = new EmbedBuilder().WithTitle("🎮 プロセカ監視設定一覧").WithColor(Color.Blue);
        var components = new ComponentBuilder();

        foreach (var c in configs)
        {
            embed.AddField($"監視: <#{c.MonitorChannelId}>", $"対象: <#{c.TargetChannelId}>\n形式: {c.OriginalNameFormat}");
            components.WithButton("削除", $"delete_prsk:{c.MonitorChannelId}", ButtonStyle.Danger);
        }

        await RespondAsync(embed: embed.Build(), components: components.Build(), ephemeral: true);
    }

    // --- メッセージ監視イベント (InteractionHandlerやProgram.csから呼び出す) ---
    // ※実際にはこのロジックを別のHandlerクラスに置くのが理想ですが、まずはここに記述します。
    public async Task OnMessageReceived(SocketMessage message)
    {
        if (message.Author.IsBot) return;

        // 5-6桁の数字が含まれているか正規表現でチェック
        var match = Regex.Match(message.Content, @"\b(\d{5,6})\b");
        if (!match.Success) return;

        string roomId = match.Groups[1].Value;

        // DBから設定を取得
        var config = await _db.GetConfigByMonitorChannelAsync(message.Channel.Id);
        if (config == null) return;

        // 対象チャンネルの名前を変更
        var targetChannel = await _client.GetChannelAsync(config.TargetChannelId) as ITextChannel;
        if (targetChannel != null)
        {
            string newName = config.OriginalNameFormat.Replace("roomid", roomId);
            await targetChannel.ModifyAsync(x => x.Name = newName);
            
            // リアクションを付与
            await message.AddReactionAsync(new Emoji("🐾"));
        }
    }
}

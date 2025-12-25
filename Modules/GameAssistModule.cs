using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Discord.Data;

namespace Discord.Modules;

public class GameAssistModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly DataService _db;
    private readonly DiscordSocketClient _client;

    public GameAssistModule(DataService db, DiscordSocketClient client)
    {
        _db = db;
        _client = client;
    }

    [SlashCommand("game-assist", "ボイスチャンネルの自動作成を設定します")]
    public async Task SetAssist(IVoiceChannel monitor, IVoiceChannel target, string nameFormat = "{0}の部屋")
    {
        var config = new GameRoomConfig
        {
            GuildId = Context.Guild.Id,
            MonitorChannelId = monitor.Id,
            TargetChannelId = target.Id,
            OriginalNameFormat = nameFormat
        };

        await _db.SaveGameRoomConfigAsync(config);
        await RespondAsync("設定を保存しました。");
    }
}

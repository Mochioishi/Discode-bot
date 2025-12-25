using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordTimeSignal.Data;
using DiscordTimeSignal.Handlers;
using DiscordTimeSignal.Modules;
using DiscordTimeSignal.Workers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;

var builder = Host.CreateApplicationBuilder(args);

// --- 1. 設定の読み込み ---
var config = builder.Configuration;

// --- 2. 依存関係の登録 (DI) ---
builder.Services.AddSingleton<DiscordSocketClient>(new DiscordSocketClient(new DiscordSocketConfig
{
    GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent | GatewayIntents.GuildMembers,
    AlwaysDownloadUsers = true
}));
builder.Services.AddSingleton<InteractionService>();
builder.Services.AddSingleton<DataService>();
builder.Services.AddSingleton<InteractionHandler>();

// --- 3. バックグラウンドサービスの登録 ---
builder.Services.AddHostedService<TimeSignalWorker>(); // bottextとdeleteagoの実行用

var host = builder.Build();

// --- 4. イベントの紐付けロジック ---
var client = host.Services.GetRequiredService<DiscordSocketClient>();
var handler = host.Services.GetRequiredService<InteractionHandler>();
var dataService = host.Services.GetRequiredService<DataService>();

// スラッシュコマンド等の初期化
await handler.InitializeAsync();

// [prsk_roomid] メッセージ監視イベントの紐付け
client.MessageReceived += async (msg) => 
{
    using var scope = host.Services.CreateScope();
    var gameModule = new GameAssistModule(dataService, client);
    await gameModule.OnMessageReceived(msg);
};

// [rolegive] リアクション監視イベントの紐付け
client.ReactionAdded += async (cache, ch, reaction) => 
{
    if (reaction.User.Value.IsBot) return;
    var cfg = await dataService.GetRoleGiveConfigAsync(reaction.MessageId, reaction.Emote.Name);
    if (cfg != null && reaction.User.Value is IGuildUser user) await user.AddRoleAsync(cfg.RoleId);
};

client.ReactionRemoved += async (cache, ch, reaction) => 
{
    if (reaction.User.Value.IsBot) return;
    var cfg = await dataService.GetRoleGiveConfigAsync(reaction.MessageId, reaction.Emote.Name);
    if (cfg != null && reaction.User.Value is IGuildUser user) await user.RemoveRoleAsync(cfg.RoleId);
};

// 起動ログ
client.Log += (log) => { Console.WriteLine(log); return Task.CompletedTask; };

// --- 5. 実行 ---
await client.LoginAsync(TokenType.Bot, config["DiscordToken"]);
await client.StartAsync();

await host.RunAsync();

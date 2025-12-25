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

// --- 1. 依存関係の登録 (DI) ---
builder.Services.AddSingleton<DiscordSocketClient>(new DiscordSocketClient(new DiscordSocketConfig
{
    GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent | GatewayIntents.GuildMembers,
    AlwaysDownloadUsers = true
}));
builder.Services.AddSingleton<InteractionService>();
builder.Services.AddSingleton<DataService>();
builder.Services.AddSingleton<InteractionHandler>();

// バックグラウンドサービス (1分ごとの監視)
builder.Services.AddHostedService<TimeSignalWorker>();

var host = builder.Build();

// --- 2. 各サービスの取得 ---
var client = host.Services.GetRequiredService<DiscordSocketClient>();
var handler = host.Services.GetRequiredService<InteractionHandler>();
var dataService = host.Services.GetRequiredService<DataService>();
var config = host.Services.GetRequiredService<IConfiguration>();

// --- 3. イベントの紐付け ---

// スラッシュコマンドとボタン等の初期化
await handler.InitializeAsync();

// [prsk_roomid] 5-6桁の数字監視
client.MessageReceived += async (msg) => 
{
    if (msg.Author.IsBot) return;
    var gameModule = new GameAssistModule(dataService, client);
    await gameModule.OnMessageReceived(msg);
};

// [rolegive] リアクション追加
client.ReactionAdded += async (cache, ch, reaction) => 
{
    if (reaction.User.Value.IsBot) return;
    var cfg = await dataService.GetRoleGiveConfigAsync(reaction.MessageId, reaction.Emote.Name);
    if (cfg != null && reaction.User.Value is IGuildUser user) await user.AddRoleAsync(cfg.RoleId);
};

// [rolegive] リアクション削除
client.ReactionRemoved += async (cache, ch, reaction) => 
{
    if (reaction.User.Value.IsBot) return;
    var cfg = await dataService.GetRoleGiveConfigAsync(reaction.MessageId, reaction.Emote.Name);
    if (cfg != null && reaction.User.Value is IGuildUser user) await user.RemoveRoleAsync(cfg.RoleId);
};

// ログ出力
client.Log += (log) => { Console.WriteLine(log); return Task.CompletedTask; };

// --- 4. ログインと起動 ---
// Railwayの環境変数 DISCORD_TOKEN を読み込む
var token = config["DISCORD_TOKEN"]; 
await client.LoginAsync(TokenType.Bot, token);
await client.StartAsync();

await host.RunAsync();

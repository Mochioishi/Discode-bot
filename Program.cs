using Discord;
using Discord.WebSocket;
using DiscordTimeSignal;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// --- 依存関係の登録 (DI) ---
// これにより各ModuleでDataServiceなどを簡単に使い回せます
builder.Services.AddSingleton<DiscordSocketClient>();
builder.Services.AddSingleton<DataService>();
builder.Services.AddSingleton<InteractionHandler>();

// --- バックグラウンドサービスの登録 ---
builder.Services.AddHostedService<TimeSignalWorker>(); // 予約投稿・時報用
builder.Services.AddHostedService<CleanupWorker>();    // 定期削除用

var app = builder.Build();

// ヘルスチェック用（Render/Railway等の維持用）
app.MapGet("/", () => "Discord Bot is Running.");

await app.RunAsync();

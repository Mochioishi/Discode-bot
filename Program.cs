using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Discord_bot;
using Discord_bot.Infrastructure;
using Discord_bot.Workers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// --- 1. Discord Socket Client の設定 ---
builder.Services.AddSingleton(new DiscordSocketConfig
{
    // スラッシュコマンドには最低限以下が必要
    GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.GuildMembers,
    AlwaysDownloadUsers = true,
    LogGatewayIntentWarnings = false
});

// --- 2. 各種サービスの登録 (DI) ---
builder.Services.AddSingleton<DiscordSocketClient>();
builder.Services.AddSingleton<InteractionService>();
builder.Services.AddSingleton<InteractionHandler>();

// データベース設定 (GitHubの既存クラス)
builder.Services.AddSingleton<DbConfig>();
builder.Services.AddSingleton<DbInitializer>();

// バックグラウンドサービス (Botの起動管理と時報)
builder.Services.AddHostedService<Worker>();
builder.Services.AddHostedService<TimeSignalWorker>();

var host = builder.Build();

// --- 3. データベースの初期化実行 ---
using (var scope = host.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<DbInitializer>();
    try 
    {
        await initializer.InitializeAsync();
        Console.WriteLine("[DB] Database initialization completed.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[DB] Initialization failed: {ex.Message}");
    }
}

await host.RunAsync();

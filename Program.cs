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
    // メッセージ内容を読み取るために MessageContent を追加
    GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.GuildMembers | GatewayIntents.MessageContent,
    AlwaysDownloadUsers = true,
    LogGatewayIntentWarnings = false
});

// --- 2. 各種サービスの登録 (DI) ---
builder.Services.AddSingleton<DiscordSocketClient>();
builder.Services.AddSingleton<InteractionService>();
builder.Services.AddSingleton<InteractionHandler>();

builder.Services.AddSingleton<DbConfig>();
builder.Services.AddSingleton<DbInitializer>();

builder.Services.AddHostedService<Worker>();
builder.Services.AddHostedService<TimeSignalWorker>();

var host = builder.Build();

// --- 3. データベースの初期化とグローバルコマンドの掃除 ---
using (var scope = host.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<DbInitializer>();
    var client = scope.ServiceProvider.GetRequiredService<DiscordSocketClient>();

    try 
    {
        await initializer.InitializeAsync();
        Console.WriteLine("[DB] Database initialization completed.");
        
        // 注意: ここで DeleteAllGlobalCommandsAsync を呼ぶと、
        // 以前登録してしまった「重複の原因」であるグローバルコマンドを消去できます。
        // 一度実行して重複が消えたら、この行は削除するかコメントアウトしてOKです。
        // await client.Rest.DeleteAllGlobalCommandsAsync();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[DB] Initialization failed: {ex.Message}");
    }
}

await host.RunAsync();

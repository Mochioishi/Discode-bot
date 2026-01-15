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
    // 重要: GuildMessageReactions を追加しないとリアクションを検知できません
    // MessageCacheSize を設定すると、古いメッセージへのリアクションも検知しやすくなります
    GatewayIntents = GatewayIntents.AllUnprivileged 
                     | GatewayIntents.GuildMembers 
                     | GatewayIntents.MessageContent 
                     | GatewayIntents.GuildMessageReactions,
    AlwaysDownloadUsers = true,
    MessageCacheSize = 100,
    LogGatewayIntentWarnings = false
});

// --- 2. 各種サービスの登録 (DI) ---
builder.Services.AddSingleton<DiscordSocketClient>();
builder.Services.AddSingleton<InteractionService>();
builder.Services.AddSingleton<InteractionHandler>();

builder.Services.AddSingleton<DbConfig>();
// DbInitializer は static を外したクラスとして登録（DbInitializer.cs の修正も必要です）
builder.Services.AddSingleton<DbInitializer>();

builder.Services.AddHostedService<Worker>();
builder.Services.AddHostedService<TimeSignalWorker>();

var host = builder.Build();

// --- 3. データベースの初期化 ---
using (var scope = host.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<DbInitializer>();

    try 
    {
        // initializer.cs で定義した InitializeAsync を呼び出す
        await initializer.InitializeAsync();
        Console.WriteLine("[DB] Database initialization completed.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[DB] Initialization failed: {ex.Message}");
    }
}

await host.RunAsync();

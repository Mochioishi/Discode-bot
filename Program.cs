using Discord;
using Discord.WebSocket;
using Discord.Interactions;
using Discord.Data;
using Discord.Modules;
using Discord.Workers;
using Discord.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Npgsql;

var builder = Host.CreateDefaultBuilder(args);

builder.ConfigureServices((hostContext, services) =>
{
    services.AddSingleton<DiscordSocketClient>(new DiscordSocketClient(new DiscordSocketConfig
    {
        GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.GuildMembers | GatewayIntents.MessageContent
    }));
    services.AddSingleton<InteractionService>();
    services.AddSingleton<DataService>();
    services.AddSingleton<InteractionHandler>();
    services.AddHostedService<TimeSignalWorker>();
});

var host = builder.Build();

// --- 1. データベーステーブルの自動作成 ---
var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL");
if (!string.IsNullOrEmpty(connectionString) && connectionString.Contains("://"))
{
    var uri = new Uri(connectionString);
    var userInfo = uri.UserInfo.Split(':');
    connectionString = $"Host={uri.Host};Port={uri.Port};Username={userInfo[0]};Password={userInfo[1]};Database={uri.AbsolutePath.Trim('/')};SSL Mode=Require;Trust Server Certificate=True";
}

using (var conn = new NpgsqlConnection(connectionString))
{
    await conn.OpenAsync();
    var tableSql = @"
        CREATE TABLE IF NOT EXISTS CleanupSettings (GuildId BIGINT PRIMARY KEY, ChannelId BIGINT NOT NULL, DaysBefore INT NOT NULL, ProtectionType TEXT);
        CREATE TABLE IF NOT EXISTS GameRoomConfigs (MonitorChannelId BIGINT PRIMARY KEY, GuildId BIGINT NOT NULL, TargetChannelId BIGINT NOT NULL, OriginalNameFormat TEXT);
        CREATE TABLE IF NOT EXISTS RoleGiveConfigs (MessageId BIGINT, EmojiName TEXT, RoleId BIGINT NOT NULL, PRIMARY KEY (MessageId, EmojiName));
        CREATE TABLE IF NOT EXISTS MessageTasks (Id SERIAL PRIMARY KEY, ChannelId BIGINT NOT NULL, Content TEXT NOT NULL, ScheduledTime TIMESTAMP NOT NULL);
    ";
    using var cmd = new NpgsqlCommand(tableSql, conn);
    await cmd.ExecuteNonQueryAsync();
}

// --- 2. コマンドの同期（全削除 & 全登録） ---
var client = host.Services.GetRequiredService<DiscordSocketClient>();
var interactionService = host.Services.GetRequiredService<InteractionService>();
var handler = host.Services.GetRequiredService<InteractionHandler>();

// ハンドラーの初期化（イベント登録など）
await handler.InitializeAsync();

client.Ready += async () =>
{
    // モジュールを読み込み
    await interactionService.AddModulesAsync(System.Reflection.Assembly.GetEntryAssembly(), host.Services);

    // 環境変数から SERVER_ID を取得
    if (ulong.TryParse(Environment.GetEnvironmentVariable("SERVER_ID"), out var guildId))
    {
        var guild = client.GetGuild(guildId);
        if (guild != null)
        {
            // 既存のギルドコマンドを全削除
            var oldCommands = await guild.GetApplicationCommandsAsync();
            int deletedCount = 0;
            foreach (var cmd in oldCommands)
            {
                await cmd.DeleteAsync();
                deletedCount++;
            }

            // 新規登録
            var registered = await interactionService.RegisterCommandsToGuildAsync(guildId, true);
            int registeredCount = registered.Count();

            Console.WriteLine($"[Command Sync] {deletedCount}件の古いコマンドを削除しました。");
            Console.WriteLine($"[Command Sync] {registeredCount}件のコマンドを新規登録しました！ (Server: {guildId})");
        }
        else
        {
            Console.WriteLine($"[Error] 指定されたサーバー (ID: {guildId}) が見つかりませんでした。Botがサーバーに参加しているか確認してください。");
        }
    }
    else
    {
        Console.WriteLine("[Error] SERVER_ID が設定されていないか、形式が正しくありません。");
    }
};

await host.RunAsync();

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
        // 全ての権限を要求（Developer PortalでのスイッチONが必要）
        GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.GuildMembers | GatewayIntents.MessageContent,
        AlwaysDownloadUsers = true
    }));
    services.AddSingleton<InteractionService>();
    services.AddSingleton<DataService>();
    services.AddSingleton<InteractionHandler>();
    services.AddHostedService<TimeSignalWorker>();
});

var host = builder.Build();

// サービス取得
var client = host.Services.GetRequiredService<DiscordSocketClient>();
var interactionService = host.Services.GetRequiredService<InteractionService>();
var handler = host.Services.GetRequiredService<InteractionHandler>();

// --- ログ出力強化セクション ---
client.Log += (msg) => {
    Console.WriteLine($"[Discord SDK Log] {msg.Severity}: {msg.Message} {msg.Exception}");
    return Task.CompletedTask;
};

interactionService.Log += (msg) => {
    Console.WriteLine($"[Interaction Log] {msg.Severity}: {msg.Message}");
    return Task.CompletedTask;
};
// --- ログ出力強化ここまで ---

// データベース接続文字列の構築とテーブル作成
var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL");
if (!string.IsNullOrEmpty(connectionString) && connectionString.Contains("://"))
{
    try {
        var uri = new Uri(connectionString);
        var userInfo = uri.UserInfo.Split(':');
        connectionString = $"Host={uri.Host};Port={uri.Port};Username={userInfo[0]};Password={userInfo[1]};Database={uri.AbsolutePath.Trim('/')};SSL Mode=Require;Trust Server Certificate=True";
    } catch (Exception ex) {
        Console.WriteLine($"[DB Error] 接続文字列の解析に失敗: {ex.Message}");
    }
}

try {
    Console.WriteLine("[DB] テーブルチェックを開始します...");
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
        Console.WriteLine("[DB] テーブル準備完了。");
    }
} catch (Exception ex) {
    Console.WriteLine($"[DB Critical Error] データベース接続に失敗しました: {ex.Message}");
}

// ハンドラー初期化
await handler.InitializeAsync();

// 接続イベント
client.Ready += async () =>
{
    Console.WriteLine("[System] Discordに接続しました。Readyイベントを開始します...");
    
    try {
        await interactionService.AddModulesAsync(System.Reflection.Assembly.GetEntryAssembly(), host.Services);
        
        if (ulong.TryParse(Environment.GetEnvironmentVariable("SERVER_ID"), out var guildId))
        {
            var guild = client.GetGuild(guildId);
            if (guild != null)
            {
                var oldCommands = await guild.GetApplicationCommandsAsync();
                foreach (var cmd in oldCommands) await cmd.DeleteAsync();

                var registered = await interactionService.RegisterCommandsToGuildAsync(guildId, true);
                
                Console.WriteLine($"[Command Sync] 完了: {oldCommands.Count}件削除 / {registered.Count()}件登録 (Guild: {guildId})");
            }
            else
            {
                Console.WriteLine($"[Error] サーバー(ID:{guildId})が見つかりません。Botが参加しているか確認してください。");
            }
        }
    } catch (Exception ex) {
        Console.WriteLine($"[Ready Error] 初期化中にエラーが発生しました: {ex.Message}");
    }
};

Console.WriteLine("[System] Botを起動します...");
await host.RunAsync();

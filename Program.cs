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
        // ユーザー情報やメッセージ内容を取得するための権限設定
        GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.GuildMembers | GatewayIntents.MessageContent,
        AlwaysDownloadUsers = true
    }));
    services.AddSingleton<InteractionService>();
    services.AddSingleton<DataService>();
    services.AddSingleton<InteractionHandler>();
    services.AddHostedService<TimeSignalWorker>();
});

var host = builder.Build();

// --- サービス・クライアントの取得 ---
var client = host.Services.GetRequiredService<DiscordSocketClient>();
var interactionService = host.Services.GetRequiredService<InteractionService>();
var handler = host.Services.GetRequiredService<InteractionHandler>();

// --- ログ出力設定（ここが重要です） ---
client.Log += (msg) => {
    Console.WriteLine($"[Discord SDK] {msg.Severity}: {msg.Message} {msg.Exception}");
    return Task.CompletedTask;
};

interactionService.Log += (msg) => {
    Console.WriteLine($"[Interaction SDK] {msg.Severity}: {msg.Message}");
    return Task.CompletedTask;
};

// --- 1. データベースの初期設定 ---
var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL");
if (!string.IsNullOrEmpty(connectionString) && connectionString.Contains("://"))
{
    try {
        var uri = new Uri(connectionString);
        var userInfo = uri.UserInfo.Split(':');
        connectionString = $"Host={uri.Host};Port={uri.Port};Username={userInfo[0]};Password={userInfo[1]};Database={uri.AbsolutePath.Trim('/')};SSL Mode=Require;Trust Server Certificate=True";
    } catch (Exception ex) {
        Console.WriteLine($"[DB Setup] URL解析エラー: {ex.Message}");
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
    Console.WriteLine($"[DB Error] データベース処理中にエラー: {ex.Message}");
}

// --- 2. ハンドラーの初期化 ---
await handler.InitializeAsync();

// --- 3. Readyイベント（コマンドの同期） ---
client.Ready += async () =>
{
    Console.WriteLine("[System] Discordに接続しました。コマンド同期を開始します...");
    
    try {
        // コマンドモジュールの読み込み
        await interactionService.AddModulesAsync(System.Reflection.Assembly.GetEntryAssembly(), host.Services);
        
        // ギルドIDを取得してコマンドを登録
        if (ulong.TryParse(Environment.GetEnvironmentVariable("SERVER_ID"), out var guildId))
        {
            var guild = client.GetGuild(guildId);
            if (guild != null)
            {
                // 古いギルドコマンドを全削除
                var oldCommands = await guild.GetApplicationCommandsAsync();
                foreach (var cmd in oldCommands) await cmd.DeleteAsync();

                // 新しいギルドコマンドを即時登録
                var registered = await interactionService.RegisterCommandsToGuildAsync(guildId, true);
                
                Console.WriteLine($"[Command Sync] 成功: {oldCommands.Count}件削除 / {registered.Count()}件登録 (Guild: {guildId})");
            }
            else
            {
                Console.WriteLine($"[Error] 指定されたSERVER_ID({guildId})のサーバーが見つかりません。Botが参加しているか再確認してください。");
            }
        }
    } catch (Exception ex) {
        Console.WriteLine($"[Ready Error] コマンド同期中にエラー: {ex.Message}");
    }
};

// --- 4. ログインと実行 ---
var token = Environment.GetEnvironmentVariable("DISCORD_TOKEN");
if (!string.IsNullOrEmpty(token))
{
    Console.WriteLine("[System] Discordへログインします...");
    await client.LoginAsync(TokenType.Bot, token);
    await client.StartAsync();
}
else
{
    Console.WriteLine("[Critical Error] DISCORD_TOKEN が設定されていません。");
}

Console.WriteLine("[System] アプリケーションを実行中...");
await host.RunAsync();

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
using Npgsql; // 追加

var builder = Host.CreateDefaultBuilder(args);

builder.ConfigureServices((hostContext, services) =>
{
    services.AddSingleton<DiscordSocketClient>();
    services.AddSingleton<InteractionService>();
    services.AddSingleton<DataService>();
    services.AddSingleton<InteractionHandler>();
    services.AddHostedService<TimeSignalWorker>();
});

var host = builder.Build();

// --- ここから自動テーブル作成ロジック ---
var dataService = host.Services.GetRequiredService<DataService>();
var config = host.Services.GetRequiredService<IConfiguration>();
var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL");

// DataServiceと同じ変換ロジックで接続文字列を作成
if (!string.IsNullOrEmpty(connectionString) && connectionString.Contains("://"))
{
    var uri = new Uri(connectionString);
    var userInfo = uri.UserInfo.Split(':');
    connectionString = $"Host={uri.Host};Port={uri.Port};Username={userInfo[0]};Password={userInfo[1]};Database={uri.AbsolutePath.Trim('/')};SSL Mode=Require;Trust Server Certificate=True";
}

Console.WriteLine("Checking database tables...");
using (var conn = new NpgsqlConnection(connectionString))
{
    await conn.OpenAsync();
    var sql = @"
        CREATE TABLE IF NOT EXISTS CleanupSettings (GuildId BIGINT PRIMARY KEY, ChannelId BIGINT NOT NULL, DaysBefore INT NOT NULL, ProtectionType TEXT);
        CREATE TABLE IF NOT EXISTS GameRoomConfigs (MonitorChannelId BIGINT PRIMARY KEY, GuildId BIGINT NOT NULL, TargetChannelId BIGINT NOT NULL, OriginalNameFormat TEXT);
        CREATE TABLE IF NOT EXISTS RoleGiveConfigs (MessageId BIGINT, EmojiName TEXT, RoleId BIGINT NOT NULL, PRIMARY KEY (MessageId, EmojiName));
        CREATE TABLE IF NOT EXISTS MessageTasks (Id SERIAL PRIMARY KEY, ChannelId BIGINT NOT NULL, Content TEXT NOT NULL, ScheduledTime TIMESTAMP NOT NULL);
    ";
    using var cmd = new NpgsqlCommand(sql, conn);
    await cmd.ExecuteNonQueryAsync();
    Console.WriteLine("Database tables are ready!");
}
// --- ここまで ---

await host.RunAsync();

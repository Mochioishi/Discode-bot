using Discord;
using Discord.WebSocket;
using Npgsql;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;

public class Worker : BackgroundService
{
    private readonly DiscordSocketClient _client;
    private readonly string _connectionString;

    public Worker(DiscordSocketClient client)
    {
        _client = client;
        _connectionString = DbConfig.GetConnectionString();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Botが準備完了するまで少し待機
        await Task.Delay(5000, stoppingToken);
        Console.WriteLine("Worker active with TimeZone: Asia/Tokyo");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                var now = DateTimeOffset.UtcNow;
                
                // 大文字小文字を厳密に指定したクエリ
                var selectSql = @"
                    SELECT ""MessageId"", ""ChannelId"" 
                    FROM ""ScheduledDeletions"" 
                    WHERE ""DeleteAt"" <= @Now";

                using var command = new NpgsqlCommand(selectSql, connection);
                command.Parameters.AddWithValue("Now", now);

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var messageId = (ulong)reader.GetInt64(0);
                    var channelId = (ulong)reader.GetInt64(1);

                    var channel = _client.GetChannel(channelId) as ITextChannel;
                    if (channel != null)
                    {
                        try {
                            var message = await channel.GetMessageAsync(messageId);
                            if (message != null)
                            {
                                await message.DeleteAsync();
                                Console.WriteLine($"Deleted message {messageId}");
                            }
                        } catch (Exception ex) {
                            Console.WriteLine($"Message delete failed: {ex.Message}");
                        }
                    }

                    // 処理後にDBから削除
                    using var deleteConn = new NpgsqlConnection(_connectionString);
                    await deleteConn.OpenAsync();
                    var deleteSql = @"DELETE FROM ""ScheduledDeletions"" WHERE ""MessageId"" = @MsgId";
                    using var deleteCmd = new NpgsqlCommand(deleteSql, deleteConn);
                    deleteCmd.Parameters.AddWithValue("MsgId", (long)messageId);
                    await deleteCmd.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Worker DB Error]: {ex.Message}");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}

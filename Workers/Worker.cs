using Discord;
using Discord.WebSocket;
using Npgsql;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;
using DiscordBot.Infrastructure;

namespace DiscordBot.Workers
{
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
            // 起動直後に DB 初期化が終わるのを 15秒 だけ待ちます
            await Task.Delay(15000, stoppingToken);
            Console.WriteLine("Worker checking database...");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var connection = new NpgsqlConnection(_connectionString);
                    await connection.OpenAsync();

                    var now = DateTimeOffset.UtcNow;
                    
                    // PostgreSQLの大文字小文字を確実に区別するため \" で囲みます
                    var selectSql = "SELECT \"MessageId\", \"ChannelId\" FROM \"ScheduledDeletions\" WHERE \"DeleteAt\" <= @Now";

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
                                if (message != null) await message.DeleteAsync();
                            } catch (Exception ex) {
                                Console.WriteLine($"Message delete failed: {ex.Message}");
                            }
                        }

                        // 処理が終わったレコードを消去
                        using var deleteConn = new NpgsqlConnection(_connectionString);
                        await deleteConn.OpenAsync();
                        var deleteSql = "DELETE FROM \"ScheduledDeletions\" WHERE \"MessageId\" = @MsgId";
                        using var deleteCmd = new NpgsqlCommand(deleteSql, deleteConn);
                        deleteCmd.Parameters.AddWithValue("MsgId", (long)messageId);
                        await deleteCmd.ExecuteNonQueryAsync();
                    }
                }
                catch (Exception ex)
                {
                    // ここでエラーが出ても止まらずに次へ行く
                    Console.WriteLine($"[Worker DB Error]: {ex.Message}");
                }

                // 1分ごとにチェック
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }
}

using Discord;
using Discord.WebSocket;
using DiscordBot.Infrastructure;
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
            await Task.Delay(5000, stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var connection = new NpgsqlConnection(_connectionString);
                    await connection.OpenAsync();

                    var now = DateTimeOffset.UtcNow;
                    var selectSql = @"SELECT ""MessageId"", ""ChannelId"" FROM ""ScheduledDeletions"" WHERE ""DeleteAt"" <= @Now";

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
                            var message = await channel.GetMessageAsync(messageId);
                            if (message != null) await message.DeleteAsync();
                        }

                        using var deleteConn = new NpgsqlConnection(_connectionString);
                        await deleteConn.OpenAsync();
                        var deleteSql = @"DELETE FROM ""ScheduledDeletions"" WHERE ""MessageId"" = @MsgId";
                        using var deleteCmd = new NpgsqlCommand(deleteSql, deleteConn);
                        deleteCmd.Parameters.AddWithValue("MsgId", (long)messageId);
                        await deleteCmd.ExecuteNonQueryAsync();
                    }
                }
                catch (Exception ex) { Console.WriteLine($"[Worker Error]: {ex.Message}"); }
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }
}

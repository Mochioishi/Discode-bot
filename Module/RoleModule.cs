using Discord;
using Discord.Interactions;
using Npgsql;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Discord_bot.Module
{
    public class RoleModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly string _connectionString;

        // InteractionHandler.cs でエラーが出ていた定義を追加
        public static readonly ConcurrentDictionary<ulong, (string Text, IRole Role, int Minutes)> PendingSettings = new();

        public RoleModule()
        {
            // DatabaseConfig を DbConfig に修正
            _connectionString = DbConfig.GetConnectionString();
        }

        [SlashCommand("rolegive", "メッセージを送信してリアクションでロールを付与します")]
        public async Task RoleGive(string text, IRole role, int minutes = 60)
        {
            var embed = new EmbedBuilder()
                .WithDescription(text)
                .WithColor(Color.Blue)
                .Build();

            await RespondAsync(embed: embed);
            var message = await GetOriginalResponseAsync();
            await message.AddReactionAsync(new Emoji("✅"));

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            var insertSql = @"
                INSERT INTO ""ScheduledDeletions"" (""MessageId"", ""ChannelId"", ""DeleteAt"") 
                VALUES (@MsgId, @ChId, @Time)
                ON CONFLICT (""MessageId"") DO NOTHING";

            using var command = new NpgsqlCommand(insertSql, connection);
            command.Parameters.AddWithValue("MsgId", (long)message.Id);
            command.Parameters.AddWithValue("ChId", (long)Context.Channel.Id);
            command.Parameters.AddWithValue("Time", DateTimeOffset.UtcNow.AddMinutes(minutes));

            await command.ExecuteNonQueryAsync();
        }
    }
}

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

        // InteractionHandler.cs から参照される一時記憶領域
        public static readonly ConcurrentDictionary<ulong, (string Text, IRole Role, int Minutes)> PendingSettings = new();

        public RoleModule()
        {
            // DatabaseConfig を DbConfig に修正済み
            _connectionString = DbConfig.GetConnectionString();
        }

        [SlashCommand("rolegive", "メッセージを送信してリアクションでロールを付与します")]
        public async Task RoleGive(string text, IRole role, int minutes = 60)
        {
            // 1. まず Discord にメッセージを送信
            var embed = new EmbedBuilder()
                .WithDescription(text)
                .WithColor(Color.Blue)
                .Build();

            await RespondAsync(embed: embed);
            var message = await GetOriginalResponseAsync();

            // 2. 【重要】作成したメッセージのIDと設定内容を「記憶」させる
            // これをしないと、InteractionHandler がリアクションを受け取った時に何のロールか判定できません
            PendingSettings.TryAdd(Context.User.Id, (text, role, minutes));

            // 3. ボット自身がリアクションを付ける
            await message.AddReactionAsync(new Emoji("✅"));

            // 4. データベースに「自動削除予定」を保存
            try
            {
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
            catch (Exception ex)
            {
                Console.WriteLine($"[RoleModule DB Error]: {ex.Message}");
            }
        }
    }
}

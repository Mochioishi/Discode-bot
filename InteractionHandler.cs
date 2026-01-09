using Discord;
using Discord.Interactions;
using DiscordBot.Infrastructure;
using DiscordBot.Modules; // PendingSettings 参照用
using Npgsql;
using System;
using System.Reflection;
using System.Threading.Tasks;

namespace DiscordBot.Services
{
    public class InteractionHandler
    {
        private readonly DiscordSocketClient _client;
        private readonly InteractionService _handler;
        private readonly IServiceProvider _services;
        private readonly string _connectionString;

        public InteractionHandler(DiscordSocketClient client, InteractionService handler, IServiceProvider services)
        {
            _client = client;
            _handler = handler;
            _services = services;
            _connectionString = DbConfig.GetConnectionString();

            _client.InteractionCreated += HandleInteraction;
        }

        public async Task InitializeAsync()
        {
            await _handler.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
        }

        private async Task HandleInteraction(SocketInteraction interaction)
        {
            try
            {
                // 1. ボタン操作 (MessageComponent) の判定
                if (interaction is SocketMessageComponent component)
                {
                    if (component.Data.CustomId.StartsWith("bt_del_"))
                    {
                        var idStr = component.Data.CustomId.Replace("bt_del_", "");
                        if (int.TryParse(idStr, out int id))
                        {
                            await HandleDeleteAsync(id, component);
                        }
                        return;
                    }
                }

                // 2. 通常のスラッシュコマンドの実行
                var context = new SocketInteractionContext(_client, interaction);
                await _handler.ExecuteCommandAsync(context, _services);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Interaction Error]: {ex}");
            }
        }

        private async Task HandleDeleteAsync(int id, SocketMessageComponent component)
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();
                var sql = "DELETE FROM \"BotTextSchedules\" WHERE \"Id\" = @id";
                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("id", id);
                int rows = await cmd.ExecuteNonQueryAsync();

                if (rows > 0)
                {
                    await component.UpdateAsync(msg => {
                        msg.Content = $"✅ 予約 (ID: {id}) を削除しました。";
                        msg.Components = new ComponentBuilder().Build(); // ボタンを消去
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Delete Action Error]: {ex.Message}");
                await component.RespondAsync("⚠️ 削除に失敗しました。", ephemeral: true);
            }
        }
    }
}

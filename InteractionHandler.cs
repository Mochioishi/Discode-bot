using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Infrastructure;
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
        private readonly string _conn;

        public InteractionHandler(DiscordSocketClient client, InteractionService handler, IServiceProvider services)
        {
            _client = client;
            _handler = handler;
            _services = services;
            _conn = DbConfig.GetConnectionString();
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
                if (interaction is SocketMessageComponent component && component.Data.CustomId.StartsWith("bt_del_"))
                {
                    await HandleDeleteBtn(component);
                    return;
                }
                var context = new SocketInteractionContext(_client, interaction);
                await _handler.ExecuteCommandAsync(context, _services);
            }
            catch (Exception ex) { Console.WriteLine(ex); }
        }

        private async Task HandleDeleteBtn(SocketMessageComponent component)
        {
            var id = component.Data.CustomId.Replace("bt_del_", "");
            using var conn = new NpgsqlConnection(_conn);
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand("DELETE FROM \"BotTextSchedules\" WHERE \"Id\" = @id", conn);
            cmd.Parameters.AddWithValue("id", int.Parse(id));
            if (await cmd.ExecuteNonQueryAsync() > 0)
            {
                await component.UpdateAsync(msg => {
                    msg.Content = "✅ 削除しました。";
                    msg.Components = null;
                });
            }
        }
    }
}

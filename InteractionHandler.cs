using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Npgsql;
using System;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

public class InteractionHandler
{
    private readonly DiscordSocketClient _client;
    private readonly InteractionService _handler;
    private readonly IServiceProvider _services;

    public InteractionHandler(DiscordSocketClient client, InteractionService handler, IServiceProvider services)
    {
        _client = client;
        _handler = handler;
        _services = services;

        // イベントの購読
        _client.Ready += ReadyAsync;
        _client.InteractionCreated += HandleInteraction;
        _client.MessageReceived += HandleMessageReceivedAsync; // プロセカ監視用
    }

    private async Task ReadyAsync()
    {
        // コマンドを全サーバーに登録（開発時はGuildId指定が速いが、本番はGlobal）
        await _handler.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
        await _handler.RegisterCommandsGloballyAsync();
        Console.WriteLine("Commands registered.");
    }

    private async Task HandleInteraction(SocketInteraction interaction)
    {
        var context = new SocketInteractionContext(_client, interaction);
        await _handler.ExecuteCommandAsync(context, _services);
    }

    // --- プロセカ部屋番号監視ロジック ---
    private async Task HandleMessageReceivedAsync(SocketMessage rawMessage)
    {
        if (rawMessage is not SocketUserMessage message || message.Author.IsBot) return;

        // 5桁または6桁の数字が含まれているか
        var match = Regex.Match(message.Content, @"\b\d{5,6}\b");
        if (match.Success)
        {
            var roomId = match.Value;
            
            using var conn = new NpgsqlConnection(DatabaseConfig.GetConnectionString());
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand("SELECT target_channel_id, original_name FROM prsk_settings WHERE monitor_channel_id = @mid", conn);
            cmd.Parameters.AddWithValue("mid", message.Channel.Id.ToString());

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var targetChannelId = ulong.Parse(reader.GetString(0));
                var template = reader.GetString(1);

                if (await _client.GetChannelAsync(targetChannelId) is ITextChannel targetChannel)
                {
                    string newName = template.Replace("【roomid】", roomId);
                    await targetChannel.ModifyAsync(x => x.Name = newName);
                    
                    // 完了のリアクション 🐾
                    await message.AddReactionAsync(new Emoji("🐾"));
                }
            }
        }
    }
}

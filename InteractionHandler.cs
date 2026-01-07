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

        // 全イベントの紐付け
        _client.Ready += ReadyAsync;
        _client.InteractionCreated += HandleInteraction;
        _client.MessageReceived += HandleMessageReceivedAsync; // プロセカ監視
        _client.ReactionAdded += HandleReactionAddedAsync;     // ロール付与
        _client.ReactionRemoved += HandleReactionRemovedAsync; // ロール剥奪
    }

    private string GetConn() => Environment.GetEnvironmentVariable("DATABASE_URL") ?? "Host=localhost;Username=postgres;Password=password;Database=discord_bot";

    private async Task ReadyAsync()
    {
        await _handler.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
        await _handler.RegisterCommandsGloballyAsync();
        Console.WriteLine("Commands & Events Initialized.");
    }

    private async Task HandleInteraction(SocketInteraction interaction)
    {
        var context = new SocketInteractionContext(_client, interaction);
        await _handler.ExecuteCommandAsync(context, _services);
    }

    // --- 1. プロセカ部屋番号監視 ---
    private async Task HandleMessageReceivedAsync(SocketMessage rawMessage)
    {
        if (rawMessage is not SocketUserMessage message || message.Author.IsBot) return;

        var match = Regex.Match(message.Content, @"\b\d{5,6}\b");
        if (match.Success)
        {
            using var conn = new NpgsqlConnection(GetConn());
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand("SELECT target_channel_id, original_name FROM prsk_settings WHERE monitor_channel_id = @mid", conn);
            cmd.Parameters.AddWithValue("mid", message.Channel.Id.ToString());

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var targetId = ulong.Parse(reader.GetString(0));
                var template = reader.GetString(1);

                if (await _client.GetChannelAsync(targetId) is ITextChannel targetChannel)
                {
                    await targetChannel.ModifyAsync(x => x.Name = template.Replace("【roomid】", match.Value));
                    await message.AddReactionAsync(new Emoji("🐾"));
                }
            }
        }
    }

    // --- 2. リアクションロール (付与) ---
    private async Task HandleReactionAddedAsync(Cacheable<IUserMessage, ulong> cachedMsg, Cacheable<IMessageChannel, ulong> cachedCh, SocketReaction reaction)
    {
        if (reaction.User.Value.IsBot) return;

        using var conn = new NpgsqlConnection(GetConn());
        await conn.OpenAsync();
        using var cmd = new NpgsqlCommand("SELECT role_id FROM reaction_roles WHERE message_id = @mid AND emoji_name = @ename", conn);
        cmd.Parameters.AddWithValue("mid", reaction.MessageId.ToString());
        cmd.Parameters.AddWithValue("ename", reaction.Emote.ToString());

        var result = await cmd.ExecuteScalarAsync();
        if (result != null)
        {
            var guildUser = reaction.User.Value as IGuildUser;
            var roleId = ulong.Parse(result.ToString());
            await guildUser?.AddRoleAsync(roleId);
        }
    }

    // --- 3. リアクションロール (剥奪) ---
    private async Task HandleReactionRemovedAsync(Cacheable<IUserMessage, ulong> cachedMsg, Cacheable<IMessageChannel, ulong> cachedCh, SocketReaction reaction)
    {
        // ユーザーがオフライン等でキャッシュにない場合、取得を試みる
        var user = reaction.User.IsSpecified ? reaction.User.Value : await _client.GetUserAsync(reaction.UserId);
        if (user == null || user.IsBot) return;

        using var conn = new NpgsqlConnection(GetConn());
        await conn.OpenAsync();
        using var cmd = new NpgsqlCommand("SELECT role_id FROM reaction_roles WHERE message_id = @mid AND emoji_name = @ename", conn);
        cmd.Parameters.AddWithValue("mid", reaction.MessageId.ToString());
        cmd.Parameters.AddWithValue("ename", reaction.Emote.ToString());

        var result = await cmd.ExecuteScalarAsync();
        if (result != null)
        {
            var guild = (reaction.Channel as SocketGuildChannel)?.Guild;
            var guildUser = guild?.GetUser(reaction.UserId);
            var roleId = ulong.Parse(result.ToString());
            await guildUser?.RemoveRoleAsync(roleId);
        }
    }
}

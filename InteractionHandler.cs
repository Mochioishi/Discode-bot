using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Npgsql;
using System;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Linq;

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

        // イベントの紐付け
        _client.Ready += ReadyAsync;
        _client.InteractionCreated += HandleInteraction;
        _client.MessageReceived += HandleMessageReceivedAsync;
        _client.ReactionAdded += HandleReactionAddedAsync;
        _client.ReactionRemoved += HandleReactionRemovedAsync;
    }

    // RailwayのDATABASE_URLをパースする共通メソッド
    private string GetConn()
    {
        var url = Environment.GetEnvironmentVariable("DATABASE_URL");
        if (string.IsNullOrEmpty(url)) return "Host=localhost;Username=postgres;Password=password;Database=discord_bot";

        var uri = new Uri(url);
        var userInfo = uri.UserInfo.Split(':');

        return new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port,
            Username = userInfo[0],
            Password = userInfo[1],
            Database = uri.LocalPath.TrimStart('/'),
            SslMode = SslMode.Require,
            TrustServerCertificate = true
        }.ToString();
    }

    private async Task ReadyAsync()
    {
        // モジュールの読み込み
        await _handler.AddModulesAsync(Assembly.GetEntryAssembly(), _services);

        // すべての接続済みギルドに対してコマンドを登録（ギルドコマンド仕様）
        foreach (var guild in _client.Guilds)
        {
            try
            {
                await _handler.RegisterCommandsToGuildAsync(guild.Id);
                Console.WriteLine($"[Command] Registered to guild: {guild.Name} ({guild.Id})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] Could not register to guild {guild.Id}: {ex.Message}");
            }
        }

        Console.WriteLine("Bot is ready and Guild Commands are synchronized.");
    }

    private async Task HandleInteraction(SocketInteraction interaction)
    {
        try
        {
            var context = new SocketInteractionContext(_client, interaction);
            await _handler.ExecuteCommandAsync(context, _services);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Interaction Error] {ex}");
            if (interaction.Type == InteractionType.ApplicationCommand)
                await interaction.GetOriginalResponseAsync().ContinueWith(async (msg) => await interaction.FollowupAsync("エラーが発生しました。"));
        }
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
                    // 🐾 リアクションを付けてから名前変更
                    await message.AddReactionAsync(new Emoji("🐾"));
                    await targetChannel.ModifyAsync(x => x.Name = template.Replace("【roomid】", match.Value));
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
            if (reaction.Channel is SocketGuildChannel guildChannel)
            {
                var guildUser = guildChannel.Guild.GetUser(reaction.UserId);
                var roleId = ulong.Parse(result.ToString());
                await guildUser?.RemoveRoleAsync(roleId);
            }
        }
    }
}

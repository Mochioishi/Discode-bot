using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordTimeSignal.Data;

namespace DiscordTimeSignal.Modules;

[Group("rolegive", "リアクションでロール付与/はく奪")]
public class RoleModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly DataService _data;
    private readonly DiscordSocketClient _client;

    public RoleModule(DataService data, DiscordSocketClient client)
    {
        _data = data;
        _client = client;

        // リアクションイベント登録（本来は専用ハンドラに寄せても良い）
        _client.ReactionAdded += OnReactionAdded;
        _client.ReactionRemoved += OnReactionRemoved;
    }

    [SlashCommand("set", "実行したチャンネルのメッセージにリアクションロールを設定します")]
    public async Task SetAsync(
        [Summary("message_id", "対象メッセージID")] ulong messageId,
        [Summary("role", "付与するロール")] IRole role,
        [Summary("emoji", "リアクション絵文字（省略時🐾）")] string emoji = "🐾")
    {
        if (Context.Channel is not ITextChannel textChannel)
        {
            await RespondAsync("テキストチャンネルで実行してください。", ephemeral: true);
            return;
        }

        var msg = await textChannel.GetMessageAsync(messageId);
        if (msg == null)
        {
            await RespondAsync("メッセージが見つかりません。", ephemeral: true);
            return;
        }

        await msg.AddReactionAsync(new Emoji(emoji));

        var entry = new RoleGiveEntry
        {
            Id = 0,
            GuildId = Context.Guild.Id,
            ChannelId = Context.Channel.Id,
            MessageId = messageId,
            RoleId = role.Id,
            Emoji = emoji
        };

        await _data.AddRoleGiveAsync(entry);

        await RespondAsync(
            $"メッセージ `{messageId}` にリアクション `{emoji}` でロール `{role.Name}` を付与/はく奪する設定を追加しました。",
            ephemeral: true);
    }

    private async Task OnReactionAdded(Cacheable<IUserMessage, ulong> cache, Cacheable<IMessageChannel, ulong> ch, SocketReaction reaction)
    {
        if (reaction.UserId == _client.CurrentUser.Id) return;

        if (ch.Value is not SocketTextChannel channel) return;

        var entry = await _data.GetRoleGiveByMessageAsync(channel.Guild.Id, channel.Id, reaction.MessageId);
        if (entry == null) return;

        if (reaction.Emote.ToString() != entry.Emoji) return;

        if (channel.Guild.GetUser(reaction.UserId) is not SocketGuildUser user) return;

        var role = channel.Guild.GetRole(entry.RoleId);
        if (role == null) return;

        await user.AddRoleAsync(role);
    }

    private async Task OnReactionRemoved(Cacheable<IUserMessage, ulong> cache, Cacheable<IMessageChannel, ulong> ch, SocketReaction reaction)
    {
        if (reaction.UserId == _client.CurrentUser.Id) return;

        if (ch.Value is not SocketTextChannel channel) return;

        var entry = await _data.GetRoleGiveByMessageAsync(channel.Guild.Id, channel.Id, reaction.MessageId);
        if (entry == null) return;

        if (reaction.Emote.ToString() != entry.Emoji) return;

        if (channel.Guild.GetUser(reaction.UserId) is not SocketGuildUser user) return;

        var role = channel.Guild.GetRole(entry.RoleId);
        if (role == null) return;

        await user.RemoveRoleAsync(role);
    }
}

[Group("rolegive_list", "rolegiveで登録した内容を一覧にする")]
public class RoleListModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly DataService _data;

    public RoleListModule(DataService data)
    {
        _data = data;
    }

    [SlashCommand("show", "rolegiveの一覧を表示します")]
    public async Task ShowAsync()
    {
        var entries = await _data.GetRoleGivesAsync(Context.Guild.Id, Context.Channel.Id);
        var list = entries.ToList();

        if (list.Count == 0)
        {
            await RespondAsync("このチャンネルには rolegive 設定がありません。", ephemeral: true);
            return;
        }

        var embed = new EmbedBuilder()
            .WithTitle("rolegive 設定一覧")
            .WithColor(Color.Green);

        foreach (var e in list)
        {
            embed.AddField(
                $"ID: {e.Id}",
                $"メッセージ: `{e.MessageId}` / ロール: `{e.RoleId}` / 絵文字: `{e.Emoji}`",
                inline: false);
        }

        await RespondAsync(embed: embed.Build(), ephemeral: true);
    }
}

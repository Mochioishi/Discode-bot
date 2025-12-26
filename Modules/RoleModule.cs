using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordTimeSignal.Data;

namespace DiscordTimeSignal.Modules;

public class PendingRoleGive
{
    public ulong GuildId { get; set; }
    public ulong ChannelId { get; set; }
    public ulong RoleId { get; set; }
}

public class RoleModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly DataService _data;
    private readonly DiscordSocketClient _client;

    private static readonly Dictionary<ulong, PendingRoleGive> Pending = new();

    public RoleModule(DataService data, DiscordSocketClient client)
    {
        _data = data;
        _client = client;
    }

    [SlashCommand("rolegive", "リアクションでロール付与/はく奪する設定を開始します")]
    public async Task RoleGiveAsync(
        [Summary("role", "付与するロール")] IRole role)
    {
        Pending[Context.User.Id] = new PendingRoleGive
        {
            GuildId = Context.Guild.Id,
            ChannelId = Context.Channel.Id,
            RoleId = role.Id
        };

        await RespondAsync(
            $"ロール {role.Mention} を設定します。\n" +
            $"対象にしたいメッセージに、使いたい絵文字でリアクションしてください。",
            ephemeral: true);
    }

    [SlashCommand("rolegive_list", "rolegiveで登録した内容を一覧にする")]
    public async Task RoleGiveListAsync()
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
                $"メッセージ: `{e.MessageId}`\nロール: <@&{e.RoleId}>\n絵文字: `{e.Emoji}`",
                inline: false);
        }

        await RespondAsync(embed: embed.Build(), ephemeral: true);
    }

    public async Task OnReactionAdded(
        Cacheable<IUserMessage, ulong> cache,
        Cacheable<IMessageChannel, ulong> ch,
        SocketReaction reaction)
    {
        try
        {
            if (reaction.UserId == _client.CurrentUser.Id) return;
            if (!ch.HasValue || ch.Value == null) return;

            var channel = ch.Value as SocketTextChannel;
            if (channel == null) return;

            // ① rolegive 実行直後の登録処理
            if (Pending.TryGetValue(reaction.UserId, out var pending))
            {
                if (pending.GuildId == channel.Guild.Id && pending.ChannelId == channel.Id)
                {
                    var entry = new RoleGiveEntry
                    {
                        Id = 0,
                        GuildId = pending.GuildId,
                        ChannelId = pending.ChannelId,
                        MessageId = reaction.MessageId,
                        RoleId = pending.RoleId,
                        Emoji = reaction.Emote.ToString()
                    };

                    await _data.AddRoleGiveAsync(entry);

                    var msg = await cache.GetOrDownloadAsync();
                    if (msg != null)
                        await msg.AddReactionAsync(reaction.Emote);

                    Pending.Remove(reaction.UserId);
                    return;
                }
            }

            // ② 通常のロール付与処理
            var rg = await _data.GetRoleGiveByMessageAsync(channel.Guild.Id, channel.Id, reaction.MessageId);
            if (rg == null) return;

            if (reaction.Emote.ToString() != rg.Emoji) return;

            var user = channel.Guild.GetUser(reaction.UserId);
            if (user == null) return;

            var role = channel.Guild.GetRole(rg.RoleId);
            if (role != null)
                await user.AddRoleAsync(role);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ReactionAdded ERROR] {ex}");
        }
    }

    public async Task OnReactionRemoved(
        Cacheable<IUserMessage, ulong> cache,
        Cacheable<IMessageChannel, ulong> ch,
        SocketReaction reaction)
    {
        try
        {
            if (reaction.UserId == _client.CurrentUser.Id) return;
            if (!ch.HasValue || ch.Value == null) return;

            var channel = ch.Value as SocketTextChannel;
            if (channel == null) return;

            var rg = await _data.GetRoleGiveByMessageAsync(channel.Guild.Id, channel.Id, reaction.MessageId);
            if (rg == null) return;

            if (reaction.Emote.ToString() != rg.Emoji) return;

            var user = channel.Guild.GetUser(reaction.UserId);
            if (user == null) return;

            var role = channel.Guild.GetRole(rg.RoleId);
            if (role != null)
                await user.RemoveRoleAsync(role);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ReactionRemoved ERROR] {ex}");
        }
    }
}

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

    // rolegive 実行後の「待機状態」
    private static readonly Dictionary<ulong, PendingRoleGive> Pending = new();

    public RoleModule(DataService data, DiscordSocketClient client)
    {
        _data = data;
        _client = client;

        _client.ReactionAdded += OnReactionAdded;
        _client.ReactionRemoved += OnReactionRemoved;
    }

    [SlashCommand("set", "リアクションロールを設定します")]
    public async Task SetAsync(
        [Summary("role", "付与するロール")] IRole role,
        [Summary("emoji", "リアクション絵文字（省略時🐾）")] string emoji = "🐾")
    {
        // 待機状態を保存
        Pending[Context.User.Id] = new PendingRoleGive
        {
            GuildId = Context.Guild.Id,
            ChannelId = Context.Channel.Id,
            RoleId = role.Id,
            Emoji = emoji
        };

        await RespondAsync(
            $"ロール `{role.Name}` を設定します。\n" +
            $"対象にしたいメッセージに `{emoji}` でリアクションしてください。",
            ephemeral: true);
    }

    private async Task OnReactionAdded(Cacheable<IUserMessage, ulong> cache, Cacheable<IMessageChannel, ulong> ch, SocketReaction reaction)
    {
        if (reaction.UserId == _client.CurrentUser.Id) return;
        if (ch.Value is not SocketTextChannel channel) return;

        // ① 待機状態のユーザーがリアクションしたか？
        if (Pending.TryGetValue(reaction.UserId, out var pending))
        {
            // 待機状態のギルド・チャンネルと一致しているか
            if (pending.GuildId == channel.Guild.Id && pending.ChannelId == channel.Id)
            {
                // このメッセージを rolegive の対象として登録
                var entry = new RoleGiveEntry
                {
                    Id = 0,
                    GuildId = pending.GuildId,
                    ChannelId = pending.ChannelId,
                    MessageId = reaction.MessageId,
                    RoleId = pending.RoleId,
                    Emoji = pending.Emoji
                };

                await _data.AddRoleGiveAsync(entry);

                // Bot が対象メッセージにリアクションを付ける
                var msg = await cache.GetOrDownloadAsync();
                await msg.AddReactionAsync(new Emoji(pending.Emoji));

                // 待機状態を削除
                Pending.Remove(reaction.UserId);

                return;
            }
        }

        // ② 通常の rolegive 処理（ロール付与）
        var rg = await _data.GetRoleGiveByMessageAsync(channel.Guild.Id, channel.Id, reaction.MessageId);
        if (rg == null) return;

        if (reaction.Emote.ToString() != rg.Emoji) return;

        if (channel.Guild.GetUser(reaction.UserId) is SocketGuildUser user)
        {
            var role = channel.Guild.GetRole(rg.RoleId);
            if (role != null)
                await user.AddRoleAsync(role);
        }
    }

    private async Task OnReactionRemoved(Cacheable<IUserMessage, ulong> cache, Cacheable<IMessageChannel, ulong> ch, SocketReaction reaction)
    {
        if (reaction.UserId == _client.CurrentUser.Id) return;
        if (ch.Value is not SocketTextChannel channel) return;

        var rg = await _data.GetRoleGiveByMessageAsync(channel.Guild.Id, channel.Id, reaction.MessageId);
        if (rg == null) return;

        if (reaction.Emote.ToString() != rg.Emoji) return;

        if (channel.Guild.GetUser(reaction.UserId) is SocketGuildUser user)
        {
            var role = channel.Guild.GetRole(rg.RoleId);
            if (role != null)
                await user.RemoveRoleAsync(role);
        }
    }
}

public class PendingRoleGive
{
    public ulong GuildId { get; set; }
    public ulong ChannelId { get; set; }
    public ulong RoleId { get; set; }
    public string Emoji { get; set; } = "🐾";
}

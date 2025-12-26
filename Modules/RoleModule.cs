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

    // /rolegive 実行時に返した ephemeral メッセージ
    public ulong NoticeMessageId { get; set; }
}

public class RoleModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly DataService _data;
    private readonly DiscordSocketClient _client;

    // ユーザーごとの「設定待ち」状態
    private static readonly Dictionary<ulong, PendingRoleGive> Pending = new();

    public RoleModule(DataService data, DiscordSocketClient client)
    {
        _data = data;
        _client = client;
    }

    // /rolegive
    [SlashCommand("rolegive", "リアクションでロール付与/はく奪する設定を開始します")]
    public async Task RoleGiveAsync(
        [Summary("role", "付与するロール")] IRole role)
    {
        // ① このチャンネルの既存メッセージにリアクションしてもらう方式
        var notice = await RespondAsync(
            $"ロール {role.Mention} を設定します。\n" +
            $"このチャンネル内の **既存のメッセージ** に、使いたい絵文字でリアクションしてください。\n" +
            $"リアクション後に、このメッセージが「設定完了」に変わります。",
            ephemeral: true);

        Pending[Context.User.Id] = new PendingRoleGive
        {
            GuildId = Context.Guild.Id,
            ChannelId = Context.Channel.Id,
            RoleId = role.Id,
            NoticeMessageId = notice.Id
        };
    }

    // /rolegive_list
    [SlashCommand("rolegive_list", "rolegiveで登録した内容を一覧にする")]
    public async Task RoleGiveListAsync()
    {
        var entries = await _data.GetRoleGivesByGuildAsync(Context.Guild.Id);
        var list = entries.ToList();

        if (list.Count == 0)
        {
            await RespondAsync("このサーバーには rolegive の設定がありません。", ephemeral: true);
            return;
        }

        var embed = new EmbedBuilder()
            .WithTitle("🎭 rolegive 設定一覧")
            .WithColor(Color.Blue);

        var components = new ComponentBuilder();

        foreach (var e in list)
        {
            embed.AddField(
                $"ID: {e.Id}",
                $"チャンネル: <#{e.ChannelId}>\n" +
                $"ロール: <@&{e.RoleId}>\n" +
                $"絵文字: `{e.Emoji}`",
                inline: false);

            components.WithButton(
                $"削除 {e.Id}",
                $"delete_rolegive_{e.Id}",
                ButtonStyle.Danger
            );
        }

        await RespondAsync(embed: embed.Build(), components: components.Build(), ephemeral: true);
    }

    // 削除ボタン Interaction
    [ComponentInteraction("delete_rolegive_*")]
    public async Task DeleteRoleGiveAsync(string id)
    {
        long entryId = long.Parse(id);
        await _data.DeleteRoleGiveAsync(entryId);
        await RespondAsync($"ID {entryId} を削除しました。", ephemeral: true);
    }

    // Program.cs で ReactionAdded に登録される
    public async Task OnReactionAdded(
        Cacheable<IUserMessage, ulong> cache,
        Cacheable<IMessageChannel, ulong> ch,
        SocketReaction reaction)
    {
        try
        {
            if (reaction.UserId == _client.CurrentUser.Id) return;

            var message = await cache.GetOrDownloadAsync();
            if (message == null) return;

            var channel = message.Channel as SocketTextChannel;
            if (channel == null) return;

            // ① /rolegive 実行後、ユーザーが「既存メッセージ」にリアクションしたときの登録処理
            if (Pending.TryGetValue(reaction.UserId, out var pending))
            {
                if (pending.GuildId == channel.Guild.Id &&
                    pending.ChannelId == channel.Id)
                {
                    // 対象メッセージ（ユーザーがリアクションした既存メッセージ）
                    var targetMessageId = reaction.MessageId;

                    var entry = new RoleGiveEntry
                    {
                        GuildId = pending.GuildId,
                        ChannelId = pending.ChannelId,
                        MessageId = targetMessageId,
                        RoleId = pending.RoleId,
                        Emoji = reaction.Emote.Name
                    };

                    await _data.AddRoleGiveAsync(entry);

                    // Bot も同じ絵文字でリアクション（確認の意味）
                    await message.AddReactionAsync(reaction.Emote);

                    // /rolegive 実行時の ephemeral メッセージを書き換え
                    var noticeChannel = _client.GetChannel(pending.ChannelId) as IMessageChannel;
                    if (noticeChannel != null)
                    {
                        var noticeMsg = await noticeChannel.GetMessageAsync(pending.NoticeMessageId) as IUserMessage;
                        if (noticeMsg != null)
                        {
                            await noticeMsg.ModifyAsync(m =>
                            {
                                m.Content =
                                    $"🎉 rolegive の設定が完了しました！\n" +
                                    $"絵文字: {reaction.Emote}\n" +
                                    $"ロール: <@&{pending.RoleId}>\n" +
                                    $"対象メッセージに、この絵文字でリアクションするとロールが付与/はく奪されます。";
                            });
                        }
                    }

                    Pending.Remove(reaction.UserId);
                    return;
                }
            }

            // ② 通常のロール付与処理
            var rg = await _data.GetRoleGiveByMessageAsync(channel.Guild.Id, channel.Id, reaction.MessageId);
            if (rg == null) return;

            if (reaction.Emote.Name != rg.Emoji) return;

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

            var message = await cache.GetOrDownloadAsync();
            if (message == null) return;

            var channel = message.Channel as SocketTextChannel;
            if (channel == null) return;

            var rg = await _data.GetRoleGiveByMessageAsync(channel.Guild.Id, channel.Id, reaction.MessageId);
            if (rg == null) return;

            if (reaction.Emote.Name != rg.Emoji) return;

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

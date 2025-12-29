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
    public IDiscordInteraction Interaction { get; set; } = null!;
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

    // /rolegive
    [SlashCommand("rolegive", "リアクションでロール付与/はく奪する設定を開始します")]
    public async Task RoleGiveAsync(
        [Summary("role", "付与するロール")] IRole role)
    {
        await RespondAsync(
            $"ロール {role.Mention} を設定します。\n" +
            $"このチャンネル内の **既存のメッセージ** に、使いたい絵文字でリアクションしてください。\n" +
            $"リアクション後に設定が完了します。",
            ephemeral: true);

        Pending[Context.User.Id] = new PendingRoleGive
        {
            GuildId = Context.Guild.Id,
            ChannelId = Context.Channel.Id,
            RoleId = role.Id,
            Interaction = Context.Interaction
        };
    }

    // /rolegive_list（UI 連番対応）
    [SlashCommand("rolegive_list", "rolegiveで登録した内容を一覧にする")]
    public async Task RoleGiveListAsync()
    {
        var entries = (await _data.GetRoleGivesByGuildAsync(Context.Guild.Id)).ToList();

        if (entries.Count == 0)
        {
            await RespondAsync("このサーバーには rolegive の設定がありません。", ephemeral: true);
            return;
        }

        var embed = new EmbedBuilder()
            .WithTitle("🎭 rolegive 設定一覧")
            .WithColor(Color.Blue);

        var components = new ComponentBuilder();

        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];

            embed.AddField(
                $"No.{i + 1}",
                $"チャンネル: <#{e.ChannelId}>\n" +
                $"ロール: <@&{e.RoleId}>\n" +
                $"絵文字: `{e.Emoji}`",
                inline: false);

            components.WithButton(
                $"削除 No.{i + 1}",
                $"delete_rolegive_index_{i}",
                ButtonStyle.Danger
            );
        }

        await RespondAsync(embed: embed.Build(), components: components.Build(), ephemeral: true);
    }

    // 削除ボタン Interaction（UI index → DB entry）
    [ComponentInteraction("delete_rolegive_index_*")]
    public async Task DeleteRoleGiveAsync(int index)
    {
        var entries = (await _data.GetRoleGivesByGuildAsync(Context.Guild.Id)).ToList();

        if (index < 0 || index >= entries.Count)
        {
            await RespondAsync("指定された項目が存在しません。", ephemeral: true);
            return;
        }

        var entry = entries[index];

        var guild = Context.Guild;
        var channel = guild.GetTextChannel(entry.ChannelId);

        IUserMessage? message = null;
        if (channel != null)
            message = await channel.GetMessageAsync(entry.MessageId) as IUserMessage;

        // 絵文字復元（カスタム対応）
        IEmote emote;
        if (Emote.TryParse(entry.Emoji, out var custom))
            emote = custom;
        else
            emote = new Emoji(entry.Emoji);

        int removedCount = 0;

        // メッセージが存在する場合のみロール剥奪とリアクション削除
        if (message != null)
        {
            var users = await message.GetReactionUsersAsync(emote, 100).FlattenAsync();
            var role = guild.GetRole(entry.RoleId);

            if (role != null)
            {
                foreach (var u in users)
                {
                    if (u.IsBot) continue;

                    var gUser = guild.GetUser(u.Id);
                    if (gUser != null && gUser.Roles.Any(r => r.Id == role.Id))
                    {
                        await gUser.RemoveRoleAsync(role);
                        removedCount++;
                    }
                }
            }

            try
            {
                await message.RemoveReactionAsync(emote, _client.CurrentUser);
            }
            catch { }
        }

        // DB 削除
        await _data.DeleteRoleGiveAsync(entry.Id);

        await RespondAsync(
            $"設定を削除しました。\n" +
            $"ロール解除対象: **{removedCount}人**\n" +
            $"Bot のリアクションも削除しました。",
            ephemeral: true
        );
    }

    // ReactionAdded
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

            // ① 設定直後の登録処理
            if (Pending.TryGetValue(reaction.UserId, out var pending))
            {
                if (pending.GuildId == channel.Guild.Id &&
                    pending.ChannelId == channel.Id)
                {
                    // 絵文字を統一形式で保存（カスタム対応）
                    string emojiString =
                        reaction.Emote is Emote custom
                        ? custom.ToString() // <:name:id>
                        : reaction.Emote.ToString();

                    var entry = new RoleGiveEntry
                    {
                        GuildId = pending.GuildId,
                        ChannelId = pending.ChannelId,
                        MessageId = reaction.MessageId,
                        RoleId = pending.RoleId,
                        Emoji = emojiString
                    };

                    await _data.AddRoleGiveAsync(entry);

                    // Bot がリアクションを付ける
                    await message.AddReactionAsync(reaction.Emote);

                    // ★ Followup ephemeral（本人だけに見える）
                    await pending.Interaction.FollowupAsync(
                        "設定が完了しました！",
                        ephemeral: true
                    );

                    Pending.Remove(reaction.UserId);
                    return;
                }
            }

            // ② 通常のロール付与処理
            var rg = await _data.GetRoleGiveByMessageAsync(channel.Guild.Id, channel.Id, reaction.MessageId);
            if (rg == null) return;

            if (reaction.Emote.ToString() != rg.Emoji &&
                reaction.Emote is Emote ce &&
                ce.ToString() != rg.Emoji)
                return;

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

    // ReactionRemoved
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

            if (reaction.Emote.ToString() != rg.Emoji &&
                reaction.Emote is Emote ce &&
                ce.ToString() != rg.Emoji)
                return;

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

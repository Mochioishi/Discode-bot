using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordTimeSignal.Data;

namespace DiscordTimeSignal.Modules;

public class PendingRoleGiveV2
{
    public ulong GuildId { get; set; }
    public ulong RoleId { get; set; }
    public int Step { get; set; } = 1;
    public ulong ChannelId { get; set; }
    public ulong MessageId { get; set; }
}

public class RoleModuleV2 : InteractionModuleBase<SocketInteractionContext>
{
    private readonly DataService _data;
    private readonly DiscordSocketClient _client;

    private static readonly Dictionary<ulong, PendingRoleGiveV2> Pending = new();

    public RoleModuleV2(DataService data, DiscordSocketClient client)
    {
        _data = data;
        _client = client;
    }

    // /rolegive
    [SlashCommand("rolegive", "リアクションロールを設定します（v2）")]
    public async Task RoleGiveStart(IRole role)
    {
        Pending[Context.User.Id] = new PendingRoleGiveV2
        {
            GuildId = Context.Guild.Id,
            RoleId = role.Id,
            Step = 1
        };

        await RespondAsync(
            "ロールを付与したいメッセージのリンクを送ってください。",
            ephemeral: true
        );
    }

    // メッセージ受信（リンク → 絵文字）
    public async Task OnMessageReceived(SocketMessage msg)
    {
        if (!Pending.TryGetValue(msg.Author.Id, out var pending)) return;
        if (msg.Author.IsBot) return;

        // Step1: メッセージリンク
        if (pending.Step == 1)
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                msg.Content,
                @"https://discord.com/channels/\d+/(\d+)/(\d+)"
            );

            if (!match.Success)
            {
                await msg.Author.SendMessageAsync("メッセージリンクが正しくありません。");
                return;
            }

            pending.ChannelId = ulong.Parse(match.Groups[1].Value);
            pending.MessageId = ulong.Parse(match.Groups[2].Value);
            pending.Step = 2;

            await msg.Author.SendMessageAsync("次に、使用する絵文字を送ってください。");
            return;
        }

        // Step2: 絵文字
        if (pending.Step == 2)
        {
            string emoji = msg.Content.Trim();

            var channel = _client.GetGuild(pending.GuildId).GetTextChannel(pending.ChannelId);
            var message = await channel.GetMessageAsync(pending.MessageId) as IUserMessage;

            if (message == null)
            {
                await msg.Author.SendMessageAsync("メッセージが見つかりませんでした。");
                Pending.Remove(msg.Author.Id);
                return;
            }

            // DB 保存
            var entry = new RoleGiveEntry
            {
                GuildId = pending.GuildId,
                ChannelId = pending.ChannelId,
                MessageId = pending.MessageId,
                RoleId = pending.RoleId,
                Emoji = emoji
            };

            await _data.AddRoleGiveAsync(entry);

            // Bot がリアクションを付ける
            await message.AddReactionAsync(new Emoji(emoji));

            // ボタンを付ける（ロール付与/剥奪）
            var builder = new ComponentBuilder()
                .WithButton("ロールを付与/解除", $"rolegive_toggle_{entry.Id}", ButtonStyle.Primary);

            await message.ModifyAsync(m => m.Components = builder.Build());

            await msg.Author.SendMessageAsync("設定が完了しました！");
            Pending.Remove(msg.Author.Id);
        }
    }

    // ボタン → ロール付与/剥奪
    [ComponentInteraction("rolegive_toggle_*")]
    public async Task ToggleRoleAsync(string id)
    {
        long entryId = long.Parse(id);

        var entry = await _data.GetRoleGiveByIdAsync(entryId);
        if (entry == null)
        {
            await RespondAsync("設定が見つかりません。", ephemeral: true);
            return;
        }

        var guild = Context.Guild;
        var user = guild.GetUser(Context.User.Id);
        var role = guild.GetRole(entry.RoleId);

        if (user == null || role == null)
        {
            await RespondAsync("ロール操作に失敗しました。", ephemeral: true);
            return;
        }

        if (user.Roles.Any(r => r.Id == role.Id))
        {
            await user.RemoveRoleAsync(role);
            await RespondAsync("ロールを解除しました。", ephemeral: true);
        }
        else
        {
            await user.AddRoleAsync(role);
            await RespondAsync("ロールを付与しました。", ephemeral: true);
        }
    }
}

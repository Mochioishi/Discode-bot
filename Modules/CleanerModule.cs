using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordTimeSignal.Data;

namespace DiscordTimeSignal.Modules;

public enum ProtectMode
{
    None,
    Image,
    Reaction,
    Both
}

public class CleanerModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly DataService _data;

    public CleanerModule(DataService data)
    {
        _data = data;
    }

    // /deleteago
    [SlashCommand("deleteago", "一定期間過ぎたメッセージを自動削除する設定")]
    public async Task DeleteAgoAsync(
        [Summary("days", "何日前より前を削除するか")] int days,
        [Summary("protect", "保護対象")] ProtectMode protect = ProtectMode.None)
    {
        if (days <= 0 || days > 365)
        {
            await RespondAsync("日数は1〜365の間で指定してください。", ephemeral: true);
            return;
        }

        var entry = new DeleteAgoEntry
        {
            GuildId = Context.Guild.Id,
            ChannelId = Context.Channel.Id,
            Days = days,
            ProtectMode = protect.ToString().ToLower()
        };

        await _data.AddDeleteAgoAsync(entry);

        await RespondAsync(
            $"このチャンネルで **{days}日以前** のメッセージを自動削除します。\n" +
            $"保護対象: `{protect}`",
            ephemeral: true);
    }

    // /deleteago_list（UI 連番対応）
    [SlashCommand("deleteago_list", "deleteagoで登録した内容を一覧表示")]
    public async Task DeleteAgoListAsync()
    {
        var entries = await _data.GetAllDeleteAgoAsync();
        var list = entries
            .Where(e => e.GuildId == Context.Guild.Id)
            .ToList();

        if (list.Count == 0)
        {
            await RespondAsync("このサーバーには deleteago の設定がありません。", ephemeral: true);
            return;
        }

        var embed = new EmbedBuilder()
            .WithTitle("🗑 deleteago 設定一覧")
            .WithColor(Color.Blue);

        var components = new ComponentBuilder();

        int index = 1;

        foreach (var e in list)
        {
            embed.AddField(
                $"No.{index}",
                $"チャンネル: <#{e.ChannelId}>\n" +
                $"日数: **{e.Days}日**\n" +
                $"保護対象: `{e.ProtectMode}`",
                inline: false
            );

            components.WithButton($"削除 No.{index}", $"delete_deleteago_{e.Id}", ButtonStyle.Danger);
            components.WithButton($"編集 No.{index}", $"edit_deleteago_{e.Id}", ButtonStyle.Primary);

            index++;
        }

        await RespondAsync(embed: embed.Build(), components: components.Build(), ephemeral: true);
    }

    // 削除ボタン
    [ComponentInteraction("delete_deleteago_*")]
    public async Task DeleteDeleteAgoAsync(string id)
    {
        long entryId = long.Parse(id);
        await _data.DeleteDeleteAgoAsync(entryId);
        await RespondAsync($"ID {entryId} を削除しました。", ephemeral: true);
    }

    // 編集ボタン → Modal（"日数のみ"）
    [ComponentInteraction("edit_deleteago_*")]
    public async Task EditDeleteAgoAsync(string id)
    {
        await RespondWithModalAsync<DeleteAgoEditModal>($"edit_deleteago_modal_{id}");
    }

    // Modal の受け取り → 日数だけ更新 → SelectMenu を出す
    [ModalInteraction("edit_deleteago_modal_*")]
    public async Task EditDeleteAgoModalAsync(string id, DeleteAgoEditModal modal)
    {
        long entryId = long.Parse(id);

        // 日数だけ更新
        await _data.UpdateDeleteAgoAsync(new DeleteAgoEntry
        {
            Id = entryId,
            Days = modal.Days
        });

        // 保護モード選択メニュー
        var menu = new SelectMenuBuilder()
            .WithCustomId($"edit_deleteago_protect_{entryId}")
            .WithPlaceholder("保護対象を選択")
            .AddOption("なし", "none")
            .AddOption("画像のみ保護", "image")
            .AddOption("リアクションのみ保護", "reaction")
            .AddOption("両方保護", "both");

        var builder = new ComponentBuilder()
            .WithSelectMenu(menu);

        await RespondAsync(
            text: "保護対象を選択してください。",
            components: builder.Build(),
            ephemeral: true
        );
    }

    // SelectMenu の受け取り → ProtectMode 更新
    [ComponentInteraction("edit_deleteago_protect_*")]
    public async Task EditDeleteAgoProtectAsync(string id, string[] selected)
    {
        long entryId = long.Parse(id);
        string protect = selected[0];

        await _data.UpdateDeleteAgoAsync(new DeleteAgoEntry
        {
            Id = entryId,
            ProtectMode = protect
        });

        await RespondAsync($"更新しました。\n保護対象: `{protect}`", ephemeral: true);
    }
}

// Modal（"日数のみ"）
public class DeleteAgoEditModal : IModal
{
    public string Title => "deleteago の編集";

    [InputLabel("日数")]
    [ModalTextInput("days", placeholder: "例: 7")]
    public int Days { get; set; }
}

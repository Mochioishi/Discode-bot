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
        var entries = (await _data.GetAllDeleteAgoAsync())
            .Where(e => e.GuildId == Context.Guild.Id)
            .ToList();

        if (entries.Count == 0)
        {
            await RespondAsync("このサーバーには deleteago の設定がありません。", ephemeral: true);
            return;
        }

        var embed = new EmbedBuilder()
            .WithTitle("🗑 deleteago 設定一覧")
            .WithColor(Color.Blue);

        var components = new ComponentBuilder();

        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];

            embed.AddField(
                $"No.{i + 1}",
                $"チャンネル: <#{e.ChannelId}>\n" +
                $"日数: **{e.Days}日**\n" +
                $"保護対象: `{e.ProtectMode}`",
                inline: false
            );

            components.WithButton($"削除 No.{i + 1}", $"delete_deleteago_index_{i}", ButtonStyle.Danger);
            components.WithButton($"編集 No.{i + 1}", $"edit_deleteago_index_{i}", ButtonStyle.Primary);
        }

        await RespondAsync(embed: embed.Build(), components: components.Build(), ephemeral: true);
    }

    // 削除（UI index → DB entry）
    [ComponentInteraction("delete_deleteago_index_*")]
    public async Task DeleteDeleteAgoAsync(int index)
    {
        var entries = (await _data.GetAllDeleteAgoAsync())
            .Where(e => e.GuildId == Context.Guild.Id)
            .ToList();

        if (index < 0 || index >= entries.Count)
        {
            await RespondAsync("指定された項目が存在しません。", ephemeral: true);
            return;
        }

        var entry = entries[index];

        await _data.DeleteDeleteAgoAsync(entry.Id);

        await RespondAsync($"設定 No.{index + 1} を削除しました。", ephemeral: true);
    }

    // 編集（UI index → Modal）
    [ComponentInteraction("edit_deleteago_index_*")]
    public async Task EditDeleteAgoAsync(int index)
    {
        var entries = (await _data.GetAllDeleteAgoAsync())
            .Where(e => e.GuildId == Context.Guild.Id)
            .ToList();

        if (index < 0 || index >= entries.Count)
        {
            await RespondAsync("指定された項目が存在しません。", ephemeral: true);
            return;
        }

        var entry = entries[index];

        await RespondWithModalAsync<DeleteAgoEditModal>($"edit_deleteago_modal_{entry.Id}");
    }

    // Modal → Days 更新 → ProtectMode 選択へ
    [ModalInteraction("edit_deleteago_modal_*")]
    public async Task EditDeleteAgoModalAsync(string id, DeleteAgoEditModal modal)
    {
        long entryId = long.Parse(id);

        await _data.UpdateDeleteAgoAsync(new DeleteAgoEntry
        {
            Id = entryId,
            Days = modal.Days
        });

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

    // ProtectMode 更新
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

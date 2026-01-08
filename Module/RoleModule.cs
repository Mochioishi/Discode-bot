using Discord;
using Discord.Interactions;
using Npgsql;
using System;
using System.Threading.Tasks;

namespace DiscordBot.Modules
{
    public class RoleModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly string _connectionString = Environment.GetEnvironmentVariable("DATABASE_URL") ?? "";

        [SlashCommand("rolegive", "リアクションロール用のメッセージを送信します")]
        public async Task SendRoleGiveMessage(
            [Summary("emoji", "使用する絵文字")] string emojiStr,
            [Summary("role", "付与するロール")] IRole role,
            [Summary("text", "表示するテキスト")] string text)
        {
            // 絵文字の解析
            IEmote targetEmoji;
            if (Emoji.TryParse(emojiStr, out var emoji))
            {
                targetEmoji = emoji;
            }
            else if (Emote.TryParse(emojiStr, out var emote))
            {
                targetEmoji = emote;
            }
            else
            {
                await RespondAsync("有効な絵文字を入力してください（標準絵文字またはカスタム絵文字）。", ephemeral: true);
                return;
            }

            // メッセージ送信
            var message = await ReplyAsync($"{text}\n\nこのメッセージに {targetEmoji} でリアクションすると、{role.Mention} ロールが付与されます。");
            await message.AddReactionAsync(targetEmoji);

            // データベースに情報を保存
            using (var conn = new NpgsqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = new NpgsqlCommand(
                    "INSERT INTO ReactionRoles (MessageId, Emoji, RoleId) VALUES (@mid, @emoji, @rid)", conn))
                {
                    cmd.Parameters.AddWithValue("mid", (long)message.Id);
                    cmd.Parameters.AddWithValue("emoji", targetEmoji.ToString() ?? "");
                    cmd.Parameters.AddWithValue("rid", (long)role.Id);
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            await RespondAsync("リアクションロールメッセージを作成しました。", ephemeral: true);
        }

        [SlashCommand("roledelete", "指定したメッセージのリアクションロール設定を削除します")]
        public async Task DeleteRoleSetting([Summary("messageid", "メッセージID")] string messageIdStr)
        {
            if (!ulong.TryParse(messageIdStr, out var messageId))
            {
                await RespondAsync("正しいメッセージIDを入力してください。", ephemeral: true);
                return;
            }

            using (var conn = new NpgsqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = new NpgsqlCommand("DELETE FROM ReactionRoles WHERE MessageId = @mid", conn))
                {
                    cmd.Parameters.AddWithValue("mid", (long)messageId);
                    int rows = await cmd.ExecuteNonQueryAsync();

                    if (rows > 0)
                        await RespondAsync("設定を削除しました。", ephemeral: true);
                    else
                        await RespondAsync("該当する設定が見つかりませんでした。", ephemeral: true);
                }
            }
        }
    }
}

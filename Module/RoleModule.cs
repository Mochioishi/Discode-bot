using Discord;
using Discord.Interactions;
using Npgsql;
using System;
using System.Threading.Tasks;

namespace DiscordBot.Modules
{
    public class RoleModule : InteractionModuleBase<SocketInteractionContext>
    {
        private string GetConnectionString()
        {
            var url = Environment.GetEnvironmentVariable("DATABASE_URL");
            if (string.IsNullOrEmpty(url)) return "Host=localhost;Username=postgres;Password=password;Database=discord_bot";
            var uri = new Uri(url);
            var userInfo = uri.UserInfo.Split(':');
            return new NpgsqlConnectionStringBuilder
            {
                Host = uri.Host, Port = uri.Port, Username = userInfo[0], Password = userInfo[1],
                Database = uri.LocalPath.TrimStart('/'), SslMode = SslMode.Require, TrustServerCertificate = true
            }.ToString();
        }

        [SlashCommand("rolegive", "リアクションロールを設定します")]
        public async Task SetReactionRole(
            [Summary("message_id", "対象メッセージのID")] string messageId,
            [Summary("role", "付与するロール")] IRole role,
            [Summary("emoji", "使用する絵文字")] string emojiStr
        )
        {
            // メッセージが存在するか確認
            if (!ulong.TryParse(messageId, out var mid))
            {
                await RespondAsync("有効なメッセージIDを入力してください。", ephemeral: true);
                return;
            }

            var message = await Context.Channel.GetMessageAsync(mid);
            if (message == null)
            {
                await RespondAsync("メッセージが見つかりませんでした。このチャンネル内のメッセージIDを指定してください。", ephemeral: true);
                return;
            }

            // 絵文字のパース
            if (!Emoji.TryParse(emojiStr, out var emoji) && !Emote.TryParse(emojiStr, out var emote))
            {
                await RespondAsync("有効な絵文字を入力してください。", ephemeral: true);
                return;
            }
            IEmote targetEmoji = (IEmote)emoji ?? emote;

            try
            {
                using var conn = new NpgsqlConnection(GetConnectionString());
                await conn.OpenAsync();

                // テーブル作成
                using var createTableCmd = new NpgsqlCommand(@"
                    CREATE TABLE IF NOT EXISTS reaction_roles (
                        id SERIAL PRIMARY KEY,
                        guild_id TEXT NOT NULL,
                        message_id TEXT NOT NULL,
                        role_id TEXT NOT NULL,
                        emoji_name TEXT NOT NULL
                    );", conn);
                await createTableCmd.ExecuteNonQueryAsync();

                // DB保存
                using var cmd = new NpgsqlCommand(@"
                    INSERT INTO reaction_roles (guild_id, message_id, role_id, emoji_name)
                    VALUES (@gid, @mid, @rid, @ename)", conn);

                cmd.Parameters.AddWithValue("gid", Context.Guild.Id.ToString());
                cmd.Parameters.AddWithValue("mid", messageId);
                cmd.Parameters.AddWithValue("rid", role.Id.ToString());
                cmd.Parameters.AddWithValue("ename", targetEmoji.ToString());

                await cmd.ExecuteNonQueryAsync();

                // Botが対象メッセージにリアクションを付ける
                await message.AddReactionAsync(targetEmoji);

                await RespondAsync($"✅ リアクションロールを設定しました。\nロール: {role.Name}\n絵文字: {targetEmoji}", ephemeral: true);
            }
            catch (Exception ex)
            {
                await RespondAsync($"エラーが発生しました: {ex.Message}", ephemeral: true);
            }
        }

        [SlashCommand("rolegive_list", "設定されているリアクションロールの一覧を表示します")]
        public async Task ListRoleGive()
        {
            using var conn = new NpgsqlConnection(GetConnectionString());
            await conn.OpenAsync();

            using var cmd = new NpgsqlCommand("SELECT id, message_id, role_id, emoji_name FROM reaction_roles WHERE guild_id = @gid", conn);
            cmd.Parameters.AddWithValue("gid", Context.Guild.Id.ToString());

            using var reader = await cmd.ExecuteReaderAsync();

            var embed = new EmbedBuilder().WithTitle("🎭 リアクションロール設定一覧").WithColor(Color.Purple);
            var component = new ComponentBuilder();
            bool hasData = false;

            while (await reader.ReadAsync())
            {
                hasData = true;
                var id = reader.GetInt32(0);
                var mid = reader.GetString(1);
                var rid = reader.GetString(2);
                var ename = reader.GetString(3);

                embed.AddField($"ID: {id}", $"メッセージ: {mid}\nロール: <@&{rid}>\n絵文字: {ename}");
                component.WithButton($"削除 {id}", $"stop_role:{id}", ButtonStyle.Danger);
            }

            if (!hasData) await RespondAsync("設定はありません。", ephemeral: true);
            else await RespondAsync(embed: embed.Build(), components: component.Build(), ephemeral: true);
        }

        [ComponentInteraction("stop_role:*")]
        public async Task StopRole(string id)
        {
            using var conn = new NpgsqlConnection(GetConnectionString());
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand("DELETE FROM reaction_roles WHERE id = @id", conn);
            cmd.Parameters.AddWithValue("id", int.Parse(id));
            await cmd.ExecuteNonQueryAsync();

            await RespondAsync("設定を削除しました。", ephemeral: true);
        }
    }
}

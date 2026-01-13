using System.Reflection;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace Discord_bot
{
    public class InteractionHandler
    {
        private readonly DiscordSocketClient _client;
        private readonly InteractionService _commands;
        private readonly IServiceProvider _services;

        public InteractionHandler(DiscordSocketClient client, InteractionService commands, IServiceProvider services)
        {
            _client = client;
            _commands = commands;
            _services = services;
        }

        public async Task InitializeAsync()
        {
            // 現在の実行ファイル内にある [SlashCommand] モジュールをすべて読み込む
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);

            // イベントハンドラの紐付け
            _client.InteractionCreated += HandleInteraction;
            _client.Ready += OnReadyAsync;
            
            // ログ出力用（デバッグに便利）
            _commands.Log += LogAsync;
        }

        private async Task OnReadyAsync()
        {
            try
            {
                // 全サーバー（グローバル）にスラッシュコマンドを登録
                // 注意: 反映まで数分〜1時間かかる場合があります
                await _commands.RegisterCommandsGloballyAsync();
                
                // 特定のテストサーバーに即時反映させたい場合は以下を使用（サーバーIDを入力）
                // ulong testGuildId = 123456789012345678; 
                // await _commands.RegisterCommandsToGuildAsync(testGuildId);

                Console.WriteLine("[Interaction] Slash commands registered successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Interaction] Registration Error: {ex}");
            }
        }

        private async Task HandleInteraction(SocketInteraction interaction)
        {
            try
            {
                // コンテキストを作成し、コマンドを実行
                var context = new SocketInteractionContext(_client, interaction);
                var result = await _commands.ExecuteCommandAsync(context, _services);

                if (!result.IsSuccess)
                {
                    Console.WriteLine($"[Error] {result.Error}: {result.ErrorReason}");
                    
                    // 実行に失敗した場合、ユーザーにエラーを通知
                    if (interaction.HasResponded)
                        await interaction.FollowupAsync($"Error: {result.ErrorReason}", ephemeral: true);
                    else
                        await interaction.RespondAsync($"Error: {result.ErrorReason}", ephemeral: true);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Critical Error] {ex}");

                // タイムアウトなどで失敗した場合のフォールバック
                if (interaction.Type == InteractionType.ApplicationCommand)
                {
                    var msg = await interaction.GetOriginalResponseAsync();
                    if (msg != null) await msg.DeleteAsync();
                }
            }
        }

        private Task LogAsync(LogMessage log)
        {
            Console.WriteLine(log.ToString());
            return Task.CompletedTask;
        }
    }
}

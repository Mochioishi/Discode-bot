// Program.cs の一部
public async Task MainAsync()
{
    // 1. DB初期化（ここでテーブルが作られる）
    try {
        DbInitializer.Initialize();
    } catch (Exception ex) {
        Console.WriteLine($"DB Initialization Error: {ex.Message}");
    }

    // 2. Discord Botの起動処理へ...
    var client = new DiscordSocketClient();
    // ...以下略
}

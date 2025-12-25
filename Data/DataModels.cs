using System;

namespace DiscordTimeSignal.Data;

/// <summary>
/// bottext: 予約投稿メッセージのデータを保持します
/// </summary>
public class BotMessageTask
{
    // 一意の識別子（削除時に使用）
    public Guid Id { get; set; } = Guid.NewGuid();
    
    // 送信先チャンネルID
    public ulong ChannelId { get; set; }
    
    // 送信するテキスト内容
    public string Content { get; set; } = string.Empty;
    
    // 埋め込み(Embed)形式にするかどうか
    public bool IsEmbed { get; set; }
    
    // 埋め込み時のタイトル（任意）
    public string? EmbedTitle { get; set; }
    
    // 送信予定時刻 (例: "14:30")
    public string? ScheduledTime { get; set; }
    
    // 作成日時
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// prsk_roomid: プロセカ等のルームID監視設定を保持します
/// </summary>
public class GameRoomConfig
{
    // サーバー(Guild)のID
    public ulong GuildId { get; set; }
    
    // 数字(RoomID)を監視するチャンネルのID
    public ulong MonitorChannelId { get; set; }
    
    // 名前を変更する対象のチャンネルのID
    public ulong TargetChannelId { get; set; }
    
    // チャンネル名のフォーマット (例: "部屋【roomid】")
    public string OriginalNameFormat { get; set; } = "【roomid】";
}

/// <summary>
/// rolegive: リアクションによるロール付与設定を保持します
/// </summary>
public class RoleGiveConfig
{
    // 対象となるメッセージのID
    public ulong MessageId { get; set; }
    
    // 付与するロールのID
    public ulong RoleId { get; set; }
    
    // 反応する絵文字名またはUnicode
    public string EmojiName { get; set; } = string.Empty;
}

/// <summary>
/// deleteago: 自動削除の設定を保持します
/// </summary>
public class CleanupSetting
{
    // サーバー(Guild)のID
    public ulong GuildId { get; set; }
    
    // 対象チャンネルのID
    public ulong ChannelId { get; set; }
    
    // 何日前のメッセージを消すか
    public int DaysBefore { get; set; }
    
    // 保護対象の設定 ("none", "image", "reaction", "both")
    public string ProtectionType { get; set; } = "none";

    // 一覧表示時に便利なチャンネル名保持用（DBカラムには必須ではありません）
    public string? ChannelName { get; set; }
}

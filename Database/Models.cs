namespace DiscordBot.Database;

public class BotText { public int id { get; set; } public long channel_id { get; set; } public string content { get; set; } public string? title { get; set; } public bool is_embed { get; set; } }
public class DeleteSetting { public int id { get; set; } public long channel_id { get; set; } public int days_ago { get; set; } public string protect_type { get; set; } }
public class PrskSetting { public int id { get; set; } public long monitor_channel_id { get; set; } public long target_channel_id { get; set; } public string original_name { get; set; } }
public class RoleGiveSetting { public int id { get; set; } public long message_id { get; set; } public long role_id { get; set; } public string emoji { get; set; } }

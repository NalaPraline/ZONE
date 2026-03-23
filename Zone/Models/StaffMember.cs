using System.Text.Json.Serialization;

namespace Zone.Models;

public class StaffMember
{
    public int Id { get; set; }

    [JsonPropertyName("characterName")]
    public string CharacterName { get; set; } = "";

    [JsonPropertyName("role")]
    public string Role { get; set; } = "";

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("avatarPath")]
    public string? AvatarPath { get; set; }

    [JsonPropertyName("discordTag")]
    public string? DiscordTag { get; set; }

    [JsonPropertyName("twitchUrl")]
    public string? TwitchUrl { get; set; }

    [JsonPropertyName("twitterHandle")]
    public string? TwitterHandle { get; set; }

    [JsonPropertyName("isOnline")]
    public bool IsOnline { get; set; }

    [JsonPropertyName("contentId")]
    public ulong ContentId { get; set; }

    [JsonPropertyName("world")]
    public string? World { get; set; }

    [JsonPropertyName("color")]
    public string? Color { get; set; }

    // Runtime only — set by online detection, not stored
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsOnlineDetected { get; set; }
}

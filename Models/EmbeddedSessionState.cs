namespace Yolcu360Otomasyon.Models;

public sealed class EmbeddedSessionState
{
    public DateTimeOffset SavedAt { get; set; } = DateTimeOffset.UtcNow;
    public string CurrentUrl { get; set; } = string.Empty;
    public string Cookies { get; set; } = string.Empty;
    public Dictionary<string, string?> LocalStorage { get; set; } = new();
    public Dictionary<string, string?> SessionStorage { get; set; } = new();
}

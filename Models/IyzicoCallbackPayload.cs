namespace Yolcu360Otomasyon.Models;

public sealed class IyzicoCallbackPayload
{
    public string Token { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? ConversationId { get; init; }
    public string? ConversationData { get; init; }
    public string? PaymentId { get; init; }
}

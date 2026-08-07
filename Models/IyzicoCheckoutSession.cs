namespace Yolcu360Otomasyon.Models;

public sealed class IyzicoCheckoutSession
{
    public string ConversationId { get; init; } = string.Empty;
    public string Token { get; init; } = string.Empty;
    public string PaymentPageUrl { get; init; } = string.Empty;
    public decimal TotalAmount { get; init; }
}

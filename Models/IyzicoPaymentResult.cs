namespace Yolcu360Otomasyon.Models;

public sealed class IyzicoPaymentResult
{
    public string ConversationId { get; init; } = string.Empty;
    public string Token { get; init; } = string.Empty;
    public string ReferenceNo { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string PaymentStatus { get; init; } = string.Empty;
    public string Provider { get; init; } = "iyzico-sandbox";
    public string? CardAssociation { get; init; }
    public string? LastFourDigits { get; init; }
    public string? CardHolderName { get; init; }
    public string? ErrorMessage { get; init; }
}

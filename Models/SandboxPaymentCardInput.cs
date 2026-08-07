namespace Yolcu360Otomasyon.Models;

public sealed class SandboxPaymentCardInput
{
    public string CardHolderName { get; init; } = string.Empty;
    public string CardNumber { get; init; } = string.Empty;
    public string ExpiryMonth { get; init; } = string.Empty;
    public string ExpiryYear { get; init; } = string.Empty;
    public string Cvc { get; init; } = string.Empty;

    public string ExpiryValue => $"{ExpiryMonth}/{ExpiryYear}";
}

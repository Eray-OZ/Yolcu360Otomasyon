using Yolcu360Otomasyon.Models;

namespace Yolcu360Otomasyon;

public partial class MainWindow
{
    private SandboxPaymentCardInput BuildSandboxPaymentCardInput()
    {
        var cardHolderName = PaymentCardHolderTextBoxControl.Text?.Trim() ?? string.Empty;
        var cardNumber = PaymentCardNumberTextBoxControl.Text?.Trim() ?? string.Empty;
        var expiryMonth = PaymentExpiryMonthTextBoxControl.Text?.Trim() ?? string.Empty;
        var expiryYear = PaymentExpiryYearTextBoxControl.Text?.Trim() ?? string.Empty;
        var cvc = PaymentCvvTextBoxControl.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(cardHolderName) ||
            string.IsNullOrWhiteSpace(cardNumber) ||
            string.IsNullOrWhiteSpace(expiryMonth) ||
            string.IsNullOrWhiteSpace(expiryYear) ||
            string.IsNullOrWhiteSpace(cvc))
        {
            throw new InvalidOperationException("Test kartı alanlarının tamamı zorunlu.");
        }

        return new SandboxPaymentCardInput
        {
            CardHolderName = cardHolderName,
            CardNumber = cardNumber,
            ExpiryMonth = expiryMonth,
            ExpiryYear = expiryYear,
            Cvc = cvc
        };
    }
}

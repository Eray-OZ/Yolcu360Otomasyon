using Yolcu360Otomasyon.Models;

namespace Yolcu360Otomasyon;

public partial class MainWindow
{
    private async Task<IyzicoCheckoutSession> InitializeCheckoutSessionAsync()
    {
        SetCheckoutStatus("Ödeme sayfası hazırlanıyor...");
        return await _iyzicoPaymentService.InitializeCheckoutAsync(_activeUser!, _paymentPreviewItems);
    }

    private async Task CompleteCheckoutInBrowserAsync(
        IyzicoCheckoutSession session,
        SandboxPaymentCardInput paymentCard)
    {
        ShowBrowserSection();
        SetCheckoutStatus("Ödeme formu dolduruluyor...");

        var baService = GetBAService();
        await baService.CompleteIyzicoSandboxPaymentAsync(session.PaymentPageUrl, paymentCard);
    }

    private async Task<IyzicoPaymentResult> WaitForPaymentResultAsync(IyzicoCheckoutSession session)
    {
        SetCheckoutStatus("Ödeme onayı bekleniyor...");
        await _iyzicoPaymentService.WaitForCallbackAsync(session.Token, TimeSpan.FromMinutes(5));
        return await _iyzicoPaymentService.RetrievePaymentResultAsync(session.ConversationId, session.Token);
    }
}

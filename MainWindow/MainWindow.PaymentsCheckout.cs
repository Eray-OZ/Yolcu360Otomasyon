using Avalonia.Interactivity;
using Yolcu360Otomasyon.Models;

namespace Yolcu360Otomasyon;

public partial class MainWindow
{
    private async void ConfirmPaymentButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_activeUser is null || _paymentPreviewItems.Count == 0)
        {
            SetCheckoutStatus("Ödeme için seçili kayıt bulunamadı.");
            return;
        }

        ConfirmPaymentButton.IsEnabled = false;
        try
        {
            var paymentCard = BuildSandboxPaymentCardInput();
            var session = await InitializeCheckoutSessionAsync();
            await CompleteCheckoutInBrowserAsync(session, paymentCard);
            var paymentResult = await WaitForPaymentResultAsync(session);

            if (!string.Equals(paymentResult.Status, "success", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(paymentResult.PaymentStatus, "SUCCESS", StringComparison.OrdinalIgnoreCase))
            {
                SetCheckoutStatus($"Ödeme tamamlanmadı. Durum: {paymentResult.Status} / {paymentResult.PaymentStatus}");
                return;
            }

            await _databaseService.CreatePaymentsFromSandboxResultAsync(
                _activeUser.Id,
                _paymentPreviewItems,
                paymentResult);

            SetCheckoutStatus("iyzico sandbox ödeme kaydı oluşturuldu.");
            ClearCheckoutForm();
            ShowPaymentsSection();
            await LoadPaymentsAsync();
        }
        catch (Exception ex)
        {
            SetCheckoutStatus($"Ödeme hatası: {ex.Message}");
        }
        finally
        {
            ConfirmPaymentButton.IsEnabled = true;
        }
    }

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

    private void PrepareCheckoutSummary()
    {
        var trCulture = new System.Globalization.CultureInfo("tr-TR");
        var total = _paymentPreviewItems.Sum(item => item.Tutar);
        PaymentSummaryCollectionsTextBlock.Text = string.Join(Environment.NewLine, _paymentPreviewItems.Select(item =>
            $"{item.KoleksiyonAdi} - {item.Tutar.ToString("N2", trCulture)} TL"));
        PaymentSummaryCountTextBlock.Text = $"{_paymentPreviewItems.Count} kayıt seçildi";
        PaymentSummaryTotalTextBlock.Text = $"{total.ToString("N2", trCulture)} TL";
        SetCheckoutStatus("Ödeme iyzico sandbox sayfasında tamamlanacak.");
    }

    private void ClearCheckoutForm()
    {
        PaymentCardHolderTextBox.Text = string.Empty;
        PaymentCardNumberTextBox.Text = string.Empty;
        PaymentExpiryMonthTextBox.Text = string.Empty;
        PaymentExpiryYearTextBox.Text = string.Empty;
        PaymentCvvTextBox.Text = string.Empty;
        _paymentPreviewItems = new List<OdemeHazirlikItem>();
    }

    private SandboxPaymentCardInput BuildSandboxPaymentCardInput()
    {
        var cardHolderName = PaymentCardHolderTextBox.Text?.Trim() ?? string.Empty;
        var cardNumber = PaymentCardNumberTextBox.Text?.Trim() ?? string.Empty;
        var expiryMonth = PaymentExpiryMonthTextBox.Text?.Trim() ?? string.Empty;
        var expiryYear = PaymentExpiryYearTextBox.Text?.Trim() ?? string.Empty;
        var cvc = PaymentCvvTextBox.Text?.Trim() ?? string.Empty;

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

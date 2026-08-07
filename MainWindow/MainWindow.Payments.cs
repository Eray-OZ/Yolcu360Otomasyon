using Avalonia.Controls;
using Avalonia.Interactivity;
using Yolcu360Otomasyon.Models;
using Yolcu360Otomasyon.Services;

namespace Yolcu360Otomasyon;

public partial class MainWindow : Window
{
    private async void PaymentsTabButton_Click(object? sender, RoutedEventArgs e)
    {
        ShowPaymentsSection();
        await LoadPaymentsAsync();
    }

    private async void CreatePaymentButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_activeUser is null || _selectedCollections.Count == 0)
        {
            HistoryStatusTextBlock.Text = "Ödeme oluşturmak için en az bir kayıt seçin.";
            return;
        }

        CreatePaymentButton.IsEnabled = false;
        try
        {
            _paymentPreviewItems = await _databaseService.GetPaymentPreviewAsync(
                _activeUser.Id,
                _selectedCollections.Select(item => item.Id).ToList());

            if (_paymentPreviewItems.Count == 0)
            {
                HistoryStatusTextBlock.Text = "Ödeme için uygun kayıt bulunamadı.";
                return;
            }

            PrepareCheckoutSummary();
            ShowPaymentCheckoutSection();
        }
        catch (Exception ex)
        {
            HistoryStatusTextBlock.Text = $"Ödeme oluşturma hatası: {ex.Message}";
        }
        finally
        {
            CreatePaymentButton.IsEnabled = true;
        }
    }

    private void BackFromCheckoutButton_Click(object? sender, RoutedEventArgs e)
    {
        ShowHistorySection();
    }

    private async void ConfirmPaymentButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_activeUser is null || _paymentPreviewItems.Count == 0)
        {
            CheckoutStatusTextBlock.Text = "Ödeme için seçili kayıt bulunamadı.";
            return;
        }

        ConfirmPaymentButton.IsEnabled = false;
        BrowserAutomationService? paymentBrowser = null;
        try
        {
            var paymentCard = BuildSandboxPaymentCardInput();
            CheckoutStatusTextBlock.Text = "iyzico sandbox ödeme sayfası hazırlanıyor...";

            var session = await _iyzicoPaymentService.InitializeCheckoutAsync(_activeUser, _paymentPreviewItems);
            paymentBrowser = new BrowserAutomationService();
            paymentBrowser.ProgressChanged += BrowserAutomationService_LoginProgressChanged;
            await paymentBrowser.InitializeAsync(headless: false, restoreSession: false);
            await paymentBrowser.CompleteIyzicoSandboxPaymentAsync(session.PaymentPageUrl, paymentCard);

            CheckoutStatusTextBlock.Text =
                $"iyzico sandbox formu dolduruldu. Callback bekleniyor: {_iyzicoCallbackService.CallbackUrl}";

            await _iyzicoPaymentService.WaitForCallbackAsync(session.Token, TimeSpan.FromMinutes(5));
            var paymentResult = await _iyzicoPaymentService.RetrievePaymentResultAsync(session.ConversationId, session.Token);

            if (!string.Equals(paymentResult.Status, "success", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(paymentResult.PaymentStatus, "SUCCESS", StringComparison.OrdinalIgnoreCase))
            {
                CheckoutStatusTextBlock.Text =
                    $"Ödeme tamamlanmadı. Durum: {paymentResult.Status} / {paymentResult.PaymentStatus}";
                return;
            }

            await _databaseService.CreatePaymentsFromSandboxResultAsync(
                _activeUser.Id,
                _paymentPreviewItems.Select(item => item.KoleksiyonId).ToList(),
                paymentResult);

            CheckoutStatusTextBlock.Text = "iyzico sandbox ödeme kaydı oluşturuldu.";
            ClearCheckoutForm();
            ShowPaymentsSection();
            await LoadPaymentsAsync();
        }
        catch (Exception ex)
        {
            CheckoutStatusTextBlock.Text = $"Ödeme hatası: {ex.Message}";
        }
        finally
        {
            if (paymentBrowser is not null)
                await paymentBrowser.DisposeAsync();

            ConfirmPaymentButton.IsEnabled = true;
        }
    }

    private async Task LoadPaymentsAsync()
    {
        if (_activeUser is null)
            return;

        var payments = await _databaseService.GetPaymentsAsync(_activeUser.Id);
        PaymentsDataGrid.ItemsSource = null;
        PaymentsDataGrid.ItemsSource = payments;
        PaymentsStatusTextBlock.Text = payments.Count == 0
            ? "Ödeme kaydı bulunamadı."
            : $"{payments.Count} ödeme kaydı listelendi.";
    }

    private void PrepareCheckoutSummary()
    {
        var total = _paymentPreviewItems.Sum(item => item.Tutar);
        PaymentSummaryCollectionsTextBlock.Text = string.Join(Environment.NewLine, _paymentPreviewItems.Select(item =>
            $"{item.KoleksiyonAdi} - {item.Tutar:N2} TL"));
        PaymentSummaryCountTextBlock.Text = $"{_paymentPreviewItems.Count} kayıt seçildi";
        PaymentSummaryTotalTextBlock.Text = $"{total:N2} TL";
        CheckoutStatusTextBlock.Text = "Ödeme iyzico sandbox sayfasında tamamlanacak.";
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

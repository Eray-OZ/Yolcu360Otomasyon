using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Yolcu360Otomasyon.Models;
using Yolcu360Otomasyon.Services;

namespace Yolcu360Otomasyon;

public partial class MainWindow : Window
{
    private Border PaymentsPanel => PaymentsViewControl.FindControl<Border>("PaymentsPanel")!;
    private TextBlock PaymentsStatusTextBlock => PaymentsViewControl.FindControl<TextBlock>("PaymentsStatusTextBlock")!;
    private DataGrid PaymentsDataGrid => PaymentsViewControl.FindControl<DataGrid>("PaymentsDataGrid")!;
    private Border PaymentCheckoutPanel => PaymentsViewControl.FindControl<Border>("PaymentCheckoutPanel")!;
    private TextBlock PaymentSummaryCollectionsTextBlock => PaymentsViewControl.FindControl<TextBlock>("PaymentSummaryCollectionsTextBlock")!;
    private TextBlock PaymentSummaryCountTextBlock => PaymentsViewControl.FindControl<TextBlock>("PaymentSummaryCountTextBlock")!;
    private TextBlock PaymentSummaryTotalTextBlock => PaymentsViewControl.FindControl<TextBlock>("PaymentSummaryTotalTextBlock")!;
    private TextBox PaymentCardHolderTextBox => PaymentsViewControl.FindControl<TextBox>("PaymentCardHolderTextBox")!;
    private TextBox PaymentCardNumberTextBox => PaymentsViewControl.FindControl<TextBox>("PaymentCardNumberTextBox")!;
    private TextBox PaymentExpiryMonthTextBox => PaymentsViewControl.FindControl<TextBox>("PaymentExpiryMonthTextBox")!;
    private TextBox PaymentExpiryYearTextBox => PaymentsViewControl.FindControl<TextBox>("PaymentExpiryYearTextBox")!;
    private TextBox PaymentCvvTextBox => PaymentsViewControl.FindControl<TextBox>("PaymentCvvTextBox")!;
    private TextBlock CheckoutStatusTextBlock => PaymentsViewControl.FindControl<TextBlock>("CheckoutStatusTextBlock")!;
    private Button BackFromCheckoutButton => PaymentsViewControl.FindControl<Button>("BackFromCheckoutButton")!;
    private Button ConfirmPaymentButton => PaymentsViewControl.FindControl<Button>("ConfirmPaymentButton")!;

    private void ConfigurePaymentsViewEvents()
    {
        BackFromCheckoutButton.Click += BackFromCheckoutButton_Click;
        ConfirmPaymentButton.Click += ConfirmPaymentButton_Click;
    }

    private async void PaymentsTabButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_isAuthenticating) return;
        ShowPaymentsSection();
        await LoadPaymentsAsync();
    }

    private void CreatePaymentButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_activeUser is null || _selectedCollection is null)
        {
            SetHistoryStatus("Ödeme oluşturmak için lütfen bir koleksiyon seçin.");
            return;
        }

        var vehicle = _selectedVehicle ?? _selectedCollectionVehicles.FirstOrDefault();
        if (vehicle is null)
        {
            SetHistoryStatus("Ödeme yapmak için lütfen koleksiyon içerisinden bir araç seçin.");
            return;
        }

        CreatePaymentButton.IsEnabled = false;
        try
        {
            var vehiclePrice = ParseVehiclePrice(vehicle.Price);

            _paymentPreviewItems = new List<OdemeHazirlikItem>
            {
                new OdemeHazirlikItem
                {
                    KoleksiyonId = _selectedCollection.Id,
                    KoleksiyonAdi = $"{_selectedCollection.OzelAd} ({vehicle.Title})",
                    Tutar = vehiclePrice
                }
            };

            PrepareCheckoutSummary();
            ShowPaymentCheckoutSection();
        }
        catch (Exception ex)
        {
            SetHistoryStatus($"Ödeme oluşturma hatası: {ex.Message}");
        }
        finally
        {
            CreatePaymentButton.IsEnabled = true;
        }
    }

    private static decimal ParseVehiclePrice(string? priceText)
    {
        var parsed = DatabaseService.ParseCurrency(priceText ?? string.Empty);
        return parsed > 0 ? parsed : 100.00m;
    }

    private void BackFromCheckoutButton_Click(object? sender, RoutedEventArgs e)
    {
        ShowHistorySection();
    }

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

        var embeddedBrowser = GetEmbeddedBrowserAutomationService();
        await embeddedBrowser.CompleteIyzicoSandboxPaymentAsync(session.PaymentPageUrl, paymentCard);
    }

    private async Task<IyzicoPaymentResult> WaitForPaymentResultAsync(IyzicoCheckoutSession session)
    {
        SetCheckoutStatus("Ödeme onayı bekleniyor...");
        await _iyzicoPaymentService.WaitForCallbackAsync(session.Token, TimeSpan.FromMinutes(5));
        return await _iyzicoPaymentService.RetrievePaymentResultAsync(session.ConversationId, session.Token);
    }

    private async Task LoadPaymentsAsync()
    {
        if (_activeUser is null)
            return;

        var payments = await _databaseService.GetPaymentsAsync(_activeUser.Id);
        PaymentsDataGrid.ItemsSource = null;
        PaymentsDataGrid.ItemsSource = payments;
        SetPaymentsStatus(payments.Count == 0
            ? "Ödeme kaydı bulunamadı."
            : $"{payments.Count} ödeme kaydı listelendi.");
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

using Avalonia.Controls;
using Avalonia.Interactivity;
using Yolcu360Otomasyon.Models;

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

        var cardHolder = PaymentCardHolderTextBox.Text?.Trim() ?? string.Empty;
        var cardNumber = NormalizeDigits(PaymentCardNumberTextBox.Text);
        var expiryMonth = NormalizeDigits(PaymentExpiryMonthTextBox.Text);
        var expiryYear = NormalizeDigits(PaymentExpiryYearTextBox.Text);
        var cvv = NormalizeDigits(PaymentCvvTextBox.Text);

        if (string.IsNullOrWhiteSpace(cardHolder) ||
            cardNumber.Length != 16 ||
            expiryMonth.Length != 2 ||
            expiryYear.Length != 2 ||
            cvv.Length is < 3 or > 4)
        {
            CheckoutStatusTextBlock.Text = "Kart bilgileri eksik veya geçersiz.";
            return;
        }

        ConfirmPaymentButton.IsEnabled = false;
        try
        {
            await _databaseService.CreateFakePaymentsAsync(
                _activeUser.Id,
                _paymentPreviewItems.Select(item => item.KoleksiyonId).ToList(),
                cardHolder,
                cardNumber[^4..]);

            CheckoutStatusTextBlock.Text = "Ödeme başarıyla tamamlandı.";
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
        CheckoutStatusTextBlock.Text = "Kart bilgilerini girip ödemeyi tamamlayın.";
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
}

using Avalonia.Interactivity;

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

        ConfirmPaymentButtonControl.IsEnabled = false;
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
            ConfirmPaymentButtonControl.IsEnabled = true;
        }
    }
}

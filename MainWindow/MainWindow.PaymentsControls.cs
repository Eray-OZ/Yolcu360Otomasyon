using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Yolcu360Otomasyon;

public partial class MainWindow
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

    private void BackFromCheckoutButton_Click(object? sender, RoutedEventArgs e)
    {
        ShowHistorySection();
    }
}

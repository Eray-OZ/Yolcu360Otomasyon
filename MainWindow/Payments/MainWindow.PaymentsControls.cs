using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Yolcu360Otomasyon;

public partial class MainWindow
{
    private Border PaymentsPanelControl => PaymentsViewRootControl.FindControl<Border>("PaymentsPanel")!;
    private TextBlock PaymentsStatusTextBlockControl => PaymentsViewRootControl.FindControl<TextBlock>("PaymentsStatusTextBlock")!;
    private DataGrid PaymentsDataGridControl => PaymentsViewRootControl.FindControl<DataGrid>("PaymentsDataGrid")!;
    private Border PaymentCheckoutPanelControl => PaymentsViewRootControl.FindControl<Border>("PaymentCheckoutPanel")!;
    private TextBlock PaymentSummaryCollectionsTextBlockControl => PaymentsViewRootControl.FindControl<TextBlock>("PaymentSummaryCollectionsTextBlock")!;
    private TextBlock PaymentSummaryCountTextBlockControl => PaymentsViewRootControl.FindControl<TextBlock>("PaymentSummaryCountTextBlock")!;
    private TextBlock PaymentSummaryTotalTextBlockControl => PaymentsViewRootControl.FindControl<TextBlock>("PaymentSummaryTotalTextBlock")!;
    private TextBox PaymentCardHolderTextBoxControl => PaymentsViewRootControl.FindControl<TextBox>("PaymentCardHolderTextBox")!;
    private TextBox PaymentCardNumberTextBoxControl => PaymentsViewRootControl.FindControl<TextBox>("PaymentCardNumberTextBox")!;
    private TextBox PaymentExpiryMonthTextBoxControl => PaymentsViewRootControl.FindControl<TextBox>("PaymentExpiryMonthTextBox")!;
    private TextBox PaymentExpiryYearTextBoxControl => PaymentsViewRootControl.FindControl<TextBox>("PaymentExpiryYearTextBox")!;
    private TextBox PaymentCvvTextBoxControl => PaymentsViewRootControl.FindControl<TextBox>("PaymentCvvTextBox")!;
    private TextBlock CheckoutStatusTextBlockControl => PaymentsViewRootControl.FindControl<TextBlock>("CheckoutStatusTextBlock")!;
    private Button BackFromCheckoutButtonControl => PaymentsViewRootControl.FindControl<Button>("BackFromCheckoutButton")!;
    private Button ConfirmPaymentButtonControl => PaymentsViewRootControl.FindControl<Button>("ConfirmPaymentButton")!;

    private void ConfigurePaymentsViewEvents()
    {
        BackFromCheckoutButtonControl.Click += BackFromCheckoutButton_Click;
        ConfirmPaymentButtonControl.Click += ConfirmPaymentButton_Click;
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

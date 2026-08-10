namespace Yolcu360Otomasyon;

public partial class MainWindow
{
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
}

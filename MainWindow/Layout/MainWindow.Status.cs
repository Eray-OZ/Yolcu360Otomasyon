namespace Yolcu360Otomasyon;

public partial class MainWindow
{
    private void SetSearchStatus(string message)
    {
        SearchStatusTextBlockControl.Text = message;
    }

    private void SetHistoryStatus(string message)
    {
        HistoryStatusTextBlockControl.Text = message;
    }

    private void SetVehicleStatus(string message)
    {
        VehicleStatusTextBlockControl.Text = message;
    }

    private void SetCheckoutStatus(string message)
    {
        CheckoutStatusTextBlockControl.Text = message;
    }

    private void SetPaymentsStatus(string message)
    {
        PaymentsStatusTextBlockControl.Text = message;
    }
}

using Avalonia.Controls;

namespace Yolcu360Otomasyon;

public partial class MainWindow : Window
{
    protected override async void OnClosed(EventArgs e)
    {
        if (_browserAutomationService is not null)
            await _browserAutomationService.DisposeAsync();

        await _smsReceiverService.DisposeAsync();
        await _iyzicoCallbackService.DisposeAsync();

        base.OnClosed(e);
    }
}

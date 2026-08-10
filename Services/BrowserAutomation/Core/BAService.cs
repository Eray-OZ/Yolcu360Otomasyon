using Avalonia.Controls;

namespace Yolcu360Otomasyon.Services;

public sealed partial class BAService
{
    private readonly NativeWebView _browser;

    public event Action<string>? ProgressChanged;

    public BAService(NativeWebView browser)
    {
        _browser = browser;
    }
}

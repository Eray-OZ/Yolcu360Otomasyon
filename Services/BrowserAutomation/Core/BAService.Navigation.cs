using Avalonia.Controls;
using Avalonia.Threading;

namespace Yolcu360Otomasyon.Services;

public sealed partial class BAService
{
    public async Task NavigateAsync(string url, TimeSpan? timeout = null)
    {
        var target = new Uri(url);
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnNavigationCompleted(object? sender, WebViewNavigationCompletedEventArgs args)
        {
            Report($"Gömülü tarayıcı yükleme tamamlandı: {args.Request}");
            completion.TrySetResult(args.IsSuccess);
        }

        _browser.NavigationCompleted += OnNavigationCompleted;

        try
        {
            Report($"Gömülü tarayıcı gidiyor: {url}");
            await Dispatcher.UIThread.InvokeAsync(() => _browser.Navigate(target), DispatcherPriority.Render);

            using var timeoutCts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(45));
            await using var registration = timeoutCts.Token.Register(() => completion.TrySetCanceled(timeoutCts.Token));

            var succeeded = await completion.Task;
            if (!succeeded)
                throw new InvalidOperationException($"Sayfa yüklenemedi: {url}");
        }
        finally
        {
            _browser.NavigationCompleted -= OnNavigationCompleted;
        }
    }
}

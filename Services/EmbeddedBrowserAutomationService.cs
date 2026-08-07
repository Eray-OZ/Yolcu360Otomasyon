using Avalonia.Controls;
using Avalonia.Threading;

namespace Yolcu360Otomasyon.Services;

public sealed class EmbeddedBrowserAutomationService
{
    private readonly NativeWebView _browser;

    public EmbeddedBrowserAutomationService(NativeWebView browser)
    {
        _browser = browser;
    }

    public async Task NavigateAsync(string url, TimeSpan? timeout = null)
    {
        var target = new Uri(url);
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnNavigationCompleted(object? sender, WebViewNavigationCompletedEventArgs args)
        {
            if (args.Request == target)
                completion.TrySetResult(args.IsSuccess);
        }

        _browser.NavigationCompleted += OnNavigationCompleted;

        try
        {
            await Dispatcher.UIThread.InvokeAsync(() => _browser.Navigate(target));

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

    public Task<string?> EvaluateScriptAsync(string script)
    {
        return Dispatcher.UIThread.InvokeAsync(() => _browser.InvokeScript(script));
    }

    public async Task<string> GetTitleAsync()
    {
        return await EvaluateScriptAsync("document.title") ?? string.Empty;
    }
}

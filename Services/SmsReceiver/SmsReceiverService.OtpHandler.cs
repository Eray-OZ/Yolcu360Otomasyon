namespace Yolcu360Otomasyon.Services;

public sealed partial class SmsReceiverService
{
    public async Task<string> WaitForCodeAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            if (!string.IsNullOrWhiteSpace(_latestCode))
                return _latestCode;
        }

        var waiter = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        using var registration = timeoutCts.Token.Register(() => waiter.TrySetCanceled(timeoutCts.Token));

        lock (_sync)
        {
            _codeWaiter?.TrySetCanceled();
            _codeWaiter = waiter;
        }

        try
        {
            return await waiter.Task;
        }
        finally
        {
            lock (_sync)
            {
                if (ReferenceEquals(_codeWaiter, waiter))
                    _codeWaiter = null;
            }
        }
    }

    public void ClearLatestCode()
    {
        lock (_sync)
        {
            _latestCode = null;
        }
    }

    private static string ExtractCode(string message)
    {
        var match = OtpRegex.Match(message);
        return match.Success ? match.Value : string.Empty;
    }

    private static string? ReadFirst(System.Collections.Specialized.NameValueCollection values, params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = values[key]?.Trim();
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }
}

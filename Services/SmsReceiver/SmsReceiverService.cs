using System.Net;
using System.Text.RegularExpressions;

namespace Yolcu360Otomasyon.Services;

public sealed partial class SmsReceiverService : IAsyncDisposable
{
    private static readonly Regex OtpRegex = new(@"\b\d{4,8}\b", RegexOptions.Compiled);
    private const int MaxPortAttempts = 20;

    private readonly object _sync = new();
    private readonly List<TaskCompletionSource<string>> _waiters = [];
    private readonly int _preferredPort;
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _listenerTask;
    private string? _latestCode;

    public event Action<string>? SmsReceived;

    public int Port { get; private set; }

    public SmsReceiverService(int port = 5000)
    {
        _preferredPort = port;
        Port = port;
    }

    public Task StartAsync()
    {
        if (_listener?.IsListening == true)
            return Task.CompletedTask;

        var started = false;

        for (var offset = 0; offset < MaxPortAttempts && !started; offset++)
        {
            var candidatePort = _preferredPort + offset;

            // Attempt 1: Wildcard +
            try
            {
                var candidateListener = new HttpListener();
                candidateListener.Prefixes.Add($"http://+:{candidatePort}/");
                candidateListener.Start();
                _listener = candidateListener;
                Port = candidatePort;
                started = true;
                break;
            }
            catch
            {
            }

            // Attempt 2: Wildcard *
            try
            {
                var candidateListener = new HttpListener();
                candidateListener.Prefixes.Add($"http://*:{candidatePort}/");
                candidateListener.Start();
                _listener = candidateListener;
                Port = candidatePort;
                started = true;
                break;
            }
            catch
            {
            }

            // Attempt 3: Localhost & 127.0.0.1
            try
            {
                var candidateListener = new HttpListener();
                candidateListener.Prefixes.Add($"http://localhost:{candidatePort}/");
                candidateListener.Prefixes.Add($"http://127.0.0.1:{candidatePort}/");
                candidateListener.Start();
                _listener = candidateListener;
                Port = candidatePort;
                started = true;
                break;
            }
            catch
            {
            }
        }

        if (!started || _listener is null)
            throw new InvalidOperationException("SMS alıcısı için uygun port bulunamadı.");

        _cts = new CancellationTokenSource();
        _listenerTask = Task.Run(() => ListenLoopAsync(_cts.Token));
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            _cts?.Cancel();
            if (_listener?.IsListening == true)
                _listener.Stop();
            _listener?.Close();

            if (_listenerTask is not null)
                await _listenerTask;
        }
        catch
        {
            // no-op
        }
        finally
        {
            _cts?.Dispose();
        }
    }
}

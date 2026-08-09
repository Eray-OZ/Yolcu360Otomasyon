using System.Net;
using System.Text;
using System.Web;
using Yolcu360Otomasyon.Models;

namespace Yolcu360Otomasyon.Services;

public sealed class IyzicoCallbackService : IAsyncDisposable
{
    private const int MaxPortAttempts = 20;

    private readonly object _sync = new();
    private readonly Dictionary<string, TaskCompletionSource<IyzicoCallbackPayload>> _waiters = [];
    private readonly int _preferredPort;
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _listenerTask;

    public IyzicoCallbackService(int preferredPort = 5002)
    {
        _preferredPort = preferredPort;
        Port = preferredPort;
    }

    public int Port { get; private set; }

    public string CallbackUrl => $"http://127.0.0.1:{Port}/iyzico/callback";

    public Task StartAsync()
    {
        if (_listener?.IsListening == true)
            return Task.CompletedTask;

        for (var offset = 0; offset < MaxPortAttempts; offset++)
        {
            var port = _preferredPort + offset;
            var listener = new HttpListener();
            listener.Prefixes.Add($"http://127.0.0.1:{port}/");

            try
            {
                listener.Start();
                _listener = listener;
                Port = port;
                _cts = new CancellationTokenSource();
                _listenerTask = Task.Run(() => ListenLoopAsync(_cts.Token));
                return Task.CompletedTask;
            }
            catch (HttpListenerException ex) when (ex.ErrorCode == 48)
            {
                listener.Close();
            }
        }

        throw new InvalidOperationException("iyzico callback dinleyicisi için uygun port bulunamadı.");
    }

    public async Task<IyzicoCallbackPayload> WaitForCallbackAsync(string token, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var waiter = new TaskCompletionSource<IyzicoCallbackPayload>(TaskCreationOptions.RunContinuationsAsynchronously);

        lock (_sync)
        {
            _waiters[token] = waiter;
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);
        using var registration = timeoutCts.Token.Register(() => waiter.TrySetCanceled(timeoutCts.Token));

        try
        {
            return await waiter.Task;
        }
        finally
        {
            lock (_sync)
            {
                _waiters.Remove(token);
            }
        }
    }

    private async Task ListenLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var context = await _listener!.GetContextAsync();
                _ = Task.Run(() => HandleContextAsync(context), cancellationToken);
            }
            catch (HttpListenerException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task HandleContextAsync(HttpListenerContext context)
    {
        try
        {
            var path = context.Request.Url?.AbsolutePath?.TrimEnd('/') ?? string.Empty;
            if (!path.Equals("/iyzico/callback", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = 404;
                await WriteHtmlResponseAsync(context.Response, "<html><body>Not found.</body></html>");
                return;
            }

            var payload = await ReadPayloadAsync(context.Request);
            if (string.IsNullOrWhiteSpace(payload.Token))
            {
                context.Response.StatusCode = 400;
                await WriteHtmlResponseAsync(context.Response, "<html><body>Token bulunamadi.</body></html>");
                return;
            }

            lock (_sync)
            {
                if (_waiters.TryGetValue(payload.Token, out var waiter))
                    waiter.TrySetResult(payload);
            }

            context.Response.StatusCode = 200;
            await WriteHtmlResponseAsync(
                context.Response,
                "<html><body style=\"font-family:Arial,sans-serif;padding:24px;\"><h2>Odeme sonucu uygulamaya iletildi.</h2><p>Bu pencereyi kapatabilirsiniz.</p></body></html>");
        }
        catch
        {
            if (context.Response.OutputStream.CanWrite)
            {
                context.Response.StatusCode = 500;
                await WriteHtmlResponseAsync(context.Response, "<html><body>Beklenmeyen hata.</body></html>");
            }
        }
    }

    private static async Task<IyzicoCallbackPayload> ReadPayloadAsync(HttpListenerRequest request)
    {
        var query = request.Url is null
            ? HttpUtility.ParseQueryString(string.Empty)
            : HttpUtility.ParseQueryString(request.Url.Query);

        var form = HttpUtility.ParseQueryString(string.Empty);
        if (request.HasEntityBody)
        {
            using var reader = new StreamReader(request.InputStream, request.ContentEncoding ?? Encoding.UTF8);
            var body = await reader.ReadToEndAsync();
            if (!string.IsNullOrWhiteSpace(body))
                form = HttpUtility.ParseQueryString(body);
        }

        string? Read(string key) => form[key]?.Trim() ?? query[key]?.Trim();

        return new IyzicoCallbackPayload
        {
            Token = Read("token") ?? string.Empty,
            Status = Read("status") ?? string.Empty,
            ConversationId = Read("conversationId"),
            ConversationData = Read("conversationData"),
            PaymentId = Read("paymentId")
        };
    }

    private static async Task WriteHtmlResponseAsync(HttpListenerResponse response, string content)
    {
        var buffer = Encoding.UTF8.GetBytes(content);
        response.ContentType = "text/html; charset=utf-8";
        response.ContentLength64 = buffer.Length;
        await response.OutputStream.WriteAsync(buffer);
        response.Close();
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
    }
}

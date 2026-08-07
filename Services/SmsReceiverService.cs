using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;

namespace Yolcu360Otomasyon.Services;

public sealed class SmsReceiverService : IAsyncDisposable
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
            var candidateListener = new HttpListener();
            candidateListener.Prefixes.Add($"http://+:{candidatePort}/");

            try
            {
                candidateListener.Start();
                _listener = candidateListener;
                Port = candidatePort;
                started = true;
            }
            catch (HttpListenerException ex) when (ex.ErrorCode == 48)
            {
                candidateListener.Close();
            }
        }

        if (!started || _listener is null)
            throw new InvalidOperationException("SMS alıcısı için uygun port bulunamadı.");

        _cts = new CancellationTokenSource();
        _listenerTask = Task.Run(() => ListenLoopAsync(_cts.Token));
        return Task.CompletedTask;
    }

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
            _waiters.Add(waiter);
        }

        try
        {
            return await waiter.Task;
        }
        finally
        {
            lock (_sync)
            {
                _waiters.Remove(waiter);
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

    private async Task ListenLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            HttpListenerContext? context = null;

            try
            {
                context = await _listener!.GetContextAsync();
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
            var requestPath = context.Request.Url?.AbsolutePath?.TrimEnd('/') ?? string.Empty;
            if (!requestPath.StartsWith("/sms", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"[SMS] Desteklenmeyen yol: {context.Request.Url}");
                context.Response.StatusCode = 404;
                await WriteResponseAsync(context.Response, """{"error":"Not found."}""");
                return;
            }

            var (message, sender) = await ReadIncomingMessageAsync(context.Request);
            var code = ExtractCode(message);

            Console.WriteLine($"[SMS] Istek alindi. Sender='{sender}', Message='{message}'");
            SmsReceived?.Invoke(string.IsNullOrWhiteSpace(message)
                ? $"{sender} | SMS isteği geldi ama mesaj alanı boş. URL: {context.Request.Url}"
                : $"{sender} | {message}");

            if (string.IsNullOrWhiteSpace(code))
            {
                Console.WriteLine("[SMS] Kod ayiklanamadi.");
                context.Response.StatusCode = 400;
                await WriteResponseAsync(context.Response, """{"error":"OTP code not found in message."}""");
                return;
            }

            Console.WriteLine($"[SMS] Kod yakalandi: {code}");
            SmsReceived?.Invoke($"Kod yakalandı: {code}");

            lock (_sync)
            {
                _latestCode = code;

                foreach (var waiter in _waiters.ToArray())
                    waiter.TrySetResult(code);
            }

            context.Response.StatusCode = 200;
            await WriteResponseAsync(context.Response, $$"""{"status":"ok","code":"{{code}}"}""");
        }
        catch (JsonException)
        {
            context.Response.StatusCode = 400;
            await WriteResponseAsync(context.Response, """{"error":"Invalid JSON payload."}""");
        }
        catch
        {
            if (context.Response.OutputStream.CanWrite)
            {
                context.Response.StatusCode = 500;
                await WriteResponseAsync(context.Response, """{"error":"Internal server error."}""");
            }
        }
    }

    private static async Task<(string Message, string Sender)> ReadIncomingMessageAsync(HttpListenerRequest request)
    {
        var sender = string.Empty;
        var message = string.Empty;

        if (request.Url is not null)
        {
            var query = HttpUtility.ParseQueryString(request.Url.Query);
            sender = ReadFirst(query, "sender", "sms_sender", "from", "phone") ?? string.Empty;
            message = ReadFirst(query, "message", "sms_message", "text", "body", "sms", "msg", "message_text") ?? string.Empty;

            if (string.IsNullOrWhiteSpace(message))
            {
                var localPath = HttpUtility.UrlDecode(request.Url.AbsolutePath)?.Trim('/') ?? string.Empty;
                if (localPath.StartsWith("sms", StringComparison.OrdinalIgnoreCase))
                {
                    var pathRemainder = localPath["sms".Length..].Trim('/');
                    if (!string.IsNullOrWhiteSpace(pathRemainder))
                        message = pathRemainder;
                }
            }
        }

        if (!request.HasEntityBody)
            return (message, sender);

        using var reader = new StreamReader(request.InputStream, request.ContentEncoding ?? Encoding.UTF8);
        var body = (await reader.ReadToEndAsync()).Trim();
        if (string.IsNullOrWhiteSpace(body))
            return (message, sender);

        var contentType = request.ContentType ?? string.Empty;
        if (contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase))
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            return (
                ReadFirst(root, "message", "sms_message", "text", "body", "sms", "msg", "message_text") ?? message,
                ReadFirst(root, "sender", "sms_sender", "from", "phone") ?? sender);
        }

        if (contentType.Contains("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
        {
            var form = HttpUtility.ParseQueryString(body);
            return (
                ReadFirst(form, "message", "sms_message", "text", "body", "sms", "msg", "message_text") ?? message,
                ReadFirst(form, "sender", "sms_sender", "from", "phone") ?? sender);
        }

        return (body, sender);
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

    private static string? ReadFirst(JsonElement root, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (root.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String)
            {
                var text = value.GetString()?.Trim();
                if (!string.IsNullOrWhiteSpace(text))
                    return text;
            }
        }

        return null;
    }

    private static async Task WriteResponseAsync(HttpListenerResponse response, string content)
    {
        var buffer = Encoding.UTF8.GetBytes(content);
        response.ContentType = "application/json; charset=utf-8";
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
        finally
        {
            _cts?.Dispose();
        }
    }

}

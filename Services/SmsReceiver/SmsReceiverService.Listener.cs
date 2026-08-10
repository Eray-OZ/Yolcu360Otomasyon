using System.Net;
using System.Text;
using System.Text.Json;
using System.Web;

namespace Yolcu360Otomasyon.Services;

public sealed partial class SmsReceiverService
{
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
            var (message, sender) = await ReadIncomingMessageAsync(context.Request);
            var code = ExtractCode(message);

            SmsReceived?.Invoke(string.IsNullOrWhiteSpace(message)
                ? $"{sender} | SMS isteği geldi ama mesaj alanı boş. URL: {context.Request.Url}"
                : $"{sender} | {message}");

            if (string.IsNullOrWhiteSpace(code))
            {
                context.Response.StatusCode = 400;
                await WriteResponseAsync(context.Response, """{"error":"OTP code not found in message."}""");
                return;
            }

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

    private static async Task WriteResponseAsync(HttpListenerResponse response, string content)
    {
        var buffer = Encoding.UTF8.GetBytes(content);
        response.ContentType = "application/json; charset=utf-8";
        response.ContentLength64 = buffer.Length;
        await response.OutputStream.WriteAsync(buffer);
        response.Close();
    }
}

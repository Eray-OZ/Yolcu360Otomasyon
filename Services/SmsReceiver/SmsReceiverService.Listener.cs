using System.Net;
using System.Text;
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
            var (message, sender) = ReadIncomingMessage(context.Request);
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
                _codeWaiter?.TrySetResult(code);
            }

            context.Response.StatusCode = 200;
            await WriteResponseAsync(context.Response, $$"""{"status":"ok","code":"{{code}}"}""");
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

    private static (string Message, string Sender) ReadIncomingMessage(HttpListenerRequest request)
    {
        if (request.Url is null)
            return (string.Empty, string.Empty);

        var query = HttpUtility.ParseQueryString(request.Url.Query);
        var message = ReadFirst(query, "message", "sms_message") ?? string.Empty;
        var sender = ReadFirst(query, "sender", "sms_sender") ?? string.Empty;
        return (message, sender);
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

using Avalonia.Threading;

namespace Yolcu360Otomasyon.Services;

public sealed partial class BAService
{
    public Task<string?> EvaluateScriptAsync(string script)
    {
        return Dispatcher.UIThread.InvokeAsync(() => _browser.InvokeScript(script));
    }

    public async Task<bool> EvaluateBooleanScriptAsync(string script)
    {
        var result = await EvaluateScriptAsync(script);
        return IsScriptTrue(result);
    }

    public async Task<T?> EvaluateJsonScriptAsync<T>(string script)
    {
        var result = await EvaluateScriptAsync(script);
        if (string.IsNullOrWhiteSpace(result))
            return default;

        var cleanJson = result.Trim();
        if (cleanJson.StartsWith("\"") && cleanJson.EndsWith("\""))
        {
            try
            {
                cleanJson = System.Text.Json.JsonSerializer.Deserialize<string>(cleanJson) ?? cleanJson;
            }
            catch
            {
                // Fallback to original cleanJson if unescaping fails.
            }
        }

        return System.Text.Json.JsonSerializer.Deserialize<T>(cleanJson);
    }

    private async Task WaitForScriptTrueAsync(string script, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;

        while (DateTimeOffset.UtcNow < deadline)
        {
            var result = await EvaluateScriptAsync(script);
            if (IsScriptTrue(result))
                return;

            await Task.Delay(ScriptPollingDelay);
        }

        throw new TimeoutException($"Gömülü tarayıcı beklenen sayfa durumuna ulaşmadı. Son kontrol sonucu: {await EvaluateScriptAsync(script)}");
    }

    private void Report(string message)
    {
        ProgressChanged?.Invoke(message);
    }

    private static bool IsScriptTrue(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().Trim('"');
        return string.Equals(normalized, "true", StringComparison.OrdinalIgnoreCase);
    }

    private static string ToJson<T>(T value)
    {
        return System.Text.Json.JsonSerializer.Serialize(value);
    }

    private static string ToJson<T>(T value, System.Text.Json.JsonSerializerOptions options)
    {
        return System.Text.Json.JsonSerializer.Serialize(value, options);
    }
}

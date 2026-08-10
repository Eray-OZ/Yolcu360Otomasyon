namespace Yolcu360Otomasyon.Services;

public sealed partial class BAService
{
    public async Task<string> GetTitleAsync()
    {
        return await EvaluateScriptAsync("document.title") ?? string.Empty;
    }

    public async Task WaitForDocumentReadyAsync(TimeSpan? timeout = null)
    {
        var deadline = DateTimeOffset.UtcNow + (timeout ?? TimeSpan.FromSeconds(20));

        while (DateTimeOffset.UtcNow < deadline)
        {
            var readyState = await EvaluateScriptAsync("document.readyState");
            if (string.Equals(readyState?.Trim('"'), "complete", StringComparison.OrdinalIgnoreCase))
                return;

            await Task.Delay(ScriptPollingDelay);
        }

        throw new TimeoutException("Gömülü tarayıcı sayfa hazır durumuna geçmedi.");
    }
}

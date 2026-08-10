namespace Yolcu360Otomasyon.Services;

public sealed partial class BAService
{
    private async Task WaitForLocationSuggestionsAsync(string selector, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        string? lastResult = null;
        var selectorJson = ToJson(selector);

        while (DateTimeOffset.UtcNow < deadline)
        {
            lastResult = await EvaluateScriptAsync(
                $$"""
                (() => {
                    const items = Array.from(document.querySelectorAll({{selectorJson}}));
                    const visibleItems = items.filter(item => {
                        const rect = item.getBoundingClientRect();
                        return rect.width > 0 && rect.height > 0;
                    });
                    return JSON.stringify({
                        total: items.length,
                        visible: visibleItems.length,
                        text: visibleItems.slice(0, 3).map(item => (item.textContent || '').replace(/\s+/g, ' ').trim())
                    });
                })();
                """);

            var summary = (lastResult ?? string.Empty).Trim('"');
            Report($"Alış yeri önerileri: {summary}");

            if (summary.Contains("\"visible\":", StringComparison.OrdinalIgnoreCase) &&
                !summary.Contains("\"visible\":0", StringComparison.OrdinalIgnoreCase))
                return;

            await Task.Delay(350);
        }

        throw new TimeoutException($"Alış yeri önerileri gelmedi. Son durum: {lastResult}");
    }
}

using System.Text.Json;
using PuppeteerSharp;

namespace Yolcu360Otomasyon.Services;

public sealed partial class BrowserAutomationService
{
    private async Task WarmUpHydrationAsync()
    {
        var page = GetPage();
        await page.Mouse.MoveAsync(20, 20);
        await page.Mouse.ClickAsync(20, 20);

        try
        {
            await page.WaitForFunctionAsync(
                "() => document.readyState === 'complete' && !!document.querySelector('#inputPickUpLocation')",
                new WaitForFunctionOptions { Timeout = 10_000 });
        }
        catch
        {
        }
    }

    private async Task CloseInitialPopupAsync()
    {
        var page = GetPage();

        try
        {
            var closed = await page.EvaluateExpressionAsync<bool>(
                """
                (() => {
                    const closeButton = document.querySelector('.gs_trigger_discount_popup_close_container');
                    if (!closeButton) return false;

                    const rect = closeButton.getBoundingClientRect();
                    const style = window.getComputedStyle(closeButton);
                    const visible = rect.width > 0 &&
                        rect.height > 0 &&
                        style.display !== 'none' &&
                        style.visibility !== 'hidden';

                    if (!visible) return false;

                    closeButton.click();
                    return true;
                })();
                """);

            if (closed)
                await WaitAsync(600);
        }
        catch
        {
        }
    }

    private async Task TypePhoneNumberHumanLikeAsync(string selector, string value)
    {
        var page = GetPage();

        await WaitAsync(550);

        var point = await GetElementCenterPointAsync(selector);
        if (point.Found && point.Enabled)
        {
            await page.Mouse.MoveAsync(point.X - 26, point.Y - 8);
            await WaitAsync(140);
            await page.Mouse.MoveAsync(point.X - 10, point.Y - 3);
            await WaitAsync(110);
            await page.Mouse.MoveAsync(point.X, point.Y);
            await WaitAsync(120);
            await page.Mouse.ClickAsync(point.X, point.Y);
        }

        await WaitAsync(220);
        await page.FocusAsync(selector);
        await WaitAsync(160);

        await page.Keyboard.DownAsync("Meta");
        await page.Keyboard.PressAsync("A");
        await page.Keyboard.UpAsync("Meta");
        await WaitAsync(120);
        await page.Keyboard.PressAsync("Backspace");
        await WaitAsync(220);

        foreach (var chunk in SplitPhoneNumber(value))
        {
            await page.Keyboard.TypeAsync(chunk, new PuppeteerSharp.Input.TypeOptions { Delay = 135 });
            await WaitAsync(180);
        }

        await page.EvaluateExpressionAsync($$"""
            (() => {
                const el = document.querySelector({{JsonSerializer.Serialize(selector)}});
                if (!el) return;
                el.dispatchEvent(new Event('input', { bubbles: true }));
                el.dispatchEvent(new Event('change', { bubbles: true }));
                el.dispatchEvent(new Event('blur', { bubbles: true }));
            })();
            """);
    }

    private async Task<bool> ClickButtonByTextHumanLikeAsync(string buttonText)
    {
        var page = GetPage();
        var point = await page.EvaluateExpressionAsync<ClickPoint>($$"""
            (() => {
                const normalize = value => (value || '').replace(/\s+/g, ' ').trim().toLocaleLowerCase('tr-TR');
                const targetText = normalize({{JsonSerializer.Serialize(buttonText)}});
                const buttons = Array.from(document.querySelectorAll('button, [role="button"], input[type="submit"], input[type="button"]'));

                const target = buttons.find(button => {
                    const rect = button.getBoundingClientRect();
                    const style = window.getComputedStyle(button);
                    const visible = rect.width > 0 &&
                        rect.height > 0 &&
                        style.display !== 'none' &&
                        style.visibility !== 'hidden' &&
                        !button.disabled;

                    if (!visible) return false;

                    const text = normalize(button.textContent || button.value || button.getAttribute('aria-label') || button.getAttribute('title') || '');
                    return text === targetText;
                });

                if (!target) return { found: false, enabled: false, x: 0, y: 0, text: '' };

                target.scrollIntoView({ block: 'center', inline: 'center' });
                const rect = target.getBoundingClientRect();
                return {
                    found: true,
                    enabled: true,
                    x: rect.left + rect.width / 2,
                    y: rect.top + rect.height / 2,
                    text: (target.textContent || target.value || '').trim()
                };
            })();
            """);

        if (!point.Found || !point.Enabled)
            return false;

        await WaitAsync(420);
        await page.Mouse.MoveAsync(point.X - 30, point.Y - 10);
        await WaitAsync(120);
        await page.Mouse.MoveAsync(point.X - 12, point.Y - 4);
        await WaitAsync(100);
        await page.Mouse.MoveAsync(point.X, point.Y);
        await WaitAsync(180);
        await page.Mouse.ClickAsync(point.X, point.Y);
        await WaitAsync(450);
        return true;
    }

    private async Task ReportRecaptchaResponseAsync(IResponse response)
    {
        try
        {
            var body = await response.TextAsync();
            var compactBody = CompactForStatus(body);

            try
            {
                using var json = JsonDocument.Parse(body);
                var root = json.RootElement;

                var message = root.TryGetProperty("message", out var messageEl)
                    ? messageEl.GetString()
                    : null;

                var score = root.TryGetProperty("score", out var scoreEl) && scoreEl.ValueKind == JsonValueKind.Number
                    ? scoreEl.GetDouble().ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
                    : null;

                var error = root.TryGetProperty("error", out var errorEl)
                    ? errorEl.GetString()
                    : null;

                var summary = $"reCAPTCHA cevap: HTTP {(int)response.Status}";
                if (!string.IsNullOrWhiteSpace(error))
                    summary += $" | error: {error}";
                if (!string.IsNullOrWhiteSpace(message))
                    summary += $" | message: {message}";
                if (!string.IsNullOrWhiteSpace(score))
                    summary += $" | score: {score}";

                Report(summary);
                await ShowDebugAsync(summary);
                return;
            }
            catch (JsonException)
            {
                var summary = $"reCAPTCHA cevap: HTTP {(int)response.Status} | {compactBody}";
                Report(summary);
                await ShowDebugAsync(summary);
            }
        }
        catch (Exception ex)
        {
            Report($"reCAPTCHA cevabı okunamadı: {ex.Message}");
        }
    }

    private static string CompactForStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Boş cevap";

        var compact = value.Replace("\r", " ").Replace("\n", " ").Replace("\t", " ").Trim();
        while (compact.Contains("  ", StringComparison.Ordinal))
            compact = compact.Replace("  ", " ");

        return compact.Length <= 180 ? compact : compact[..180];
    }

    private async Task<ClickPoint> GetElementCenterPointAsync(string selector)
    {
        var page = GetPage();
        return await page.EvaluateExpressionAsync<ClickPoint>($$"""
            (() => {
                const el = document.querySelector({{JsonSerializer.Serialize(selector)}});
                if (!el) return { found: false, enabled: false, x: 0, y: 0, text: '' };

                const rect = el.getBoundingClientRect();
                const style = window.getComputedStyle(el);
                const visible = rect.width > 0 &&
                    rect.height > 0 &&
                    style.display !== 'none' &&
                    style.visibility !== 'hidden';

                return {
                    found: true,
                    enabled: visible && !el.disabled,
                    x: rect.left + rect.width / 2,
                    y: rect.top + rect.height / 2,
                    text: ''
                };
            })();
            """);
    }

    private static IEnumerable<string> SplitPhoneNumber(string value)
    {
        if (value.Length <= 3)
            return [value];

        var parts = new List<string>();
        var index = 0;

        while (index < value.Length)
        {
            var remaining = value.Length - index;
            var take = remaining > 7 ? 3 : remaining > 4 ? 2 : remaining;
            parts.Add(value.Substring(index, take));
            index += take;
        }

        return parts;
    }

    private static string NormalizePhoneNumber(string phoneNumber)
    {
        var digits = new string(phoneNumber.Where(char.IsDigit).ToArray());

        if (digits.StartsWith("90", StringComparison.Ordinal) && digits.Length == 12)
            digits = digits[2..];

        if (digits.StartsWith("0", StringComparison.Ordinal) && digits.Length == 11)
            digits = digits[1..];

        return digits;
    }

    private void Report(string message) => ProgressChanged?.Invoke(message);

    private async Task ShowDebugAsync(string message)
    {
        var page = GetPage();
        var msgJson = JsonSerializer.Serialize(message);

        await page.EvaluateExpressionAsync($$"""
            (() => {
                let panel = document.querySelector('#_y360_debug');
                if (!panel) {
                    panel = document.createElement('div');
                    panel.id = '_y360_debug';
                    Object.assign(panel.style, {
                        position: 'fixed', left: '12px', top: '12px',
                        zIndex: '2147483647', padding: '10px 14px',
                        background: '#111827', color: '#f9fafb',
                        font: '13px -apple-system, sans-serif', borderRadius: '8px',
                        boxShadow: '0 8px 24px rgba(0,0,0,.35)', maxWidth: '520px'
                    });
                    document.body.appendChild(panel);
                }
                panel.textContent = {{msgJson}};
            })();
            """);
    }

    private async Task<string> GetBodyTextAsync()
    {
        var page = GetPage();
        return await page.EvaluateExpressionAsync<string>("document.body?.innerText || ''");
    }

    private async Task<string> GetDiagnosticAsync()
    {
        var page = GetPage();
        var url = page.Url;
        var text = (await GetBodyTextAsync())
            .Replace('\n', ' ')
            .Replace('\r', ' ');

        if (text.Length > 240)
            text = text[..240];

        return $"URL: {url}. Sayfa: {text}";
    }

    private static Task WaitAsync(int ms) => Task.Delay(ms);

    private IPage GetPage()
    {
        return _page ?? throw new InvalidOperationException("InitializeAsync çağrılmadan tarayıcı kullanılamaz.");
    }

    public async ValueTask DisposeAsync()
    {
        if (_page is not null)
            await _page.CloseAsync();

        if (_browser is not null)
            await _browser.CloseAsync();
    }
}

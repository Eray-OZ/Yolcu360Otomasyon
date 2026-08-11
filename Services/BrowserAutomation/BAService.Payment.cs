using System.Text.Json;
using Yolcu360Otomasyon.Models;

namespace Yolcu360Otomasyon.Services;

public sealed partial class BAService
{
    public async Task CompleteIyzicoSandboxPaymentAsync(string paymentPageUrl, SandboxPaymentCardInput cardInput)
    {
        if (string.IsNullOrWhiteSpace(paymentPageUrl))
            throw new InvalidOperationException("iyzico ödeme sayfası adresi boş.");

        ValidateSandboxCardInput(cardInput);

        Report($"iyzico ödeme sayfası URL: {paymentPageUrl}");
        Console.WriteLine($"[Iyzico] Payment page URL: {paymentPageUrl}");

        Report("Gömülü tarayıcıda iyzico ödeme sayfası açılıyor...");
        await NavigateAsync(paymentPageUrl);
        await WaitForDocumentReadyAsync();

        Report("iyzico ödeme formu bekleniyor...");
        await WaitForScriptTrueAsync(
            """
            (() => !!document.querySelector('#ccname') || !!document.querySelector('#ccnumber') || !!document.querySelector('input[name*="card"]'))();
            """,
            TimeSpan.FromSeconds(30));

        await Task.Delay(TimeSpan.FromSeconds(10));

        // Temporary diagnostic dump. Remove after payment selectors are confirmed.
        try
        {
            var paymentHtmlJson = await EvaluateScriptAsync("JSON.stringify(document.documentElement.outerHTML)");
            var paymentHtml = JsonSerializer.Deserialize<string>(paymentHtmlJson ?? "\"\"") ?? string.Empty;
            var projectRoot = Directory.GetCurrentDirectory();
            var paymentHtmlPath = Path.Combine(projectRoot, "iyzico-payment-page.html");
            await File.WriteAllTextAsync(paymentHtmlPath, paymentHtml);
            Report($"iyzico ödeme formu HTML'i kaydedildi: {paymentHtmlPath}");
            Console.WriteLine($"[Iyzico] Payment form HTML saved to: {paymentHtmlPath}");
        }
        catch (Exception ex)
        {
            Report($"iyzico ödeme formu HTML'i kaydedilemedi: {ex.Message}");
            Console.WriteLine($"[Iyzico] Payment form HTML dump failed: {ex}");
        }

        // Ensure credit card tab is selected
        await EvaluateScriptAsync(
            """
            (() => {
                const tab = document.querySelector('#iyz-tab-credit-card');
                if (tab) tab.click();
                return true;
            })();
            """);

        await WaitForPaymentCardInputsReadyAsync(TimeSpan.FromSeconds(10));

        Report("Gömülü tarayıcıda Kart Sahibi yazılıyor...");
        await TypeIntoPaymentFieldAsync("#ccname", cardInput.CardHolderName);

        Report("Gömülü tarayıcıda Kart Numarası yazılıyor...");
        await TypeIntoPaymentFieldAsync("#ccnumber", NormalizeDigits(cardInput.CardNumber));

        Report("Gömülü tarayıcıda Son Kullanma Tarihi yazılıyor...");
        await TypeIntoPaymentFieldAsync("#ccexp", cardInput.ExpiryValue);

        Report("Gömülü tarayıcıda CVC yazılıyor...");
        await TypeIntoPaymentFieldAsync("#cccvc", NormalizeDigits(cardInput.Cvc));

        await WaitForPaymentButtonReadyAsync(TimeSpan.FromSeconds(10));

        Report("iyzico ödeme onay butonuna tıklanıyor...");
        var paymentClicked = await EvaluateBooleanScriptAsync(
            """
            (() => {
                const btn = document.querySelector('#iyz-payment-button') ||
                    Array.from(document.querySelectorAll('button, input[type="submit"]'))
                        .find(b => (b.textContent || b.value || '').trim().toLowerCase().includes('ödeme'));
                if (btn) {
                    btn.scrollIntoView({ block: 'center', inline: 'nearest' });
                    btn.click();
                    return true;
                }
                return false;
            })();
            """);

        if (!paymentClicked)
            throw new InvalidOperationException("Gömülü tarayıcıda iyzico ödeme butonu tıklanamadı.");

        Report("iyzico ödeme işlemi gömülü tarayıcıda tamamlandı.");
    }

    private async Task TypeIntoPaymentFieldAsync(string selector, string value)
    {
        var selectorJson = JsonSerializer.Serialize(selector);
        var valueJson = JsonSerializer.Serialize(value);

        await EvaluateScriptAsync(
            $$"""
            (() => {
                const input = document.querySelector({{selectorJson}});
                if (!input) return false;
                input.focus();

                const proto = input instanceof HTMLInputElement ? Object.getPrototypeOf(input) : null;
                const desc = proto ? Object.getOwnPropertyDescriptor(proto, 'value') : null;
                if (desc && desc.set) {
                    desc.set.call(input, {{valueJson}});
                } else {
                    input.value = {{valueJson}};
                }

                input.dispatchEvent(new Event('input', { bubbles: true }));
                input.dispatchEvent(new Event('change', { bubbles: true }));
                input.dispatchEvent(new Event('blur', { bubbles: true }));
                return true;
            })();
            """);

        await WaitForPaymentFieldValueAsync(selector, value, TimeSpan.FromSeconds(3));
    }

    private Task WaitForPaymentCardInputsReadyAsync(TimeSpan timeout)
    {
        return WaitForScriptTrueAsync(
            """
            (() => {
                const selectors = ['#ccname', '#ccnumber', '#ccexp', '#cccvc'];
                return selectors.every(selector => {
                    const input = document.querySelector(selector);
                    if (!input) return false;
                    const rect = input.getBoundingClientRect();
                    const style = window.getComputedStyle(input);
                    return rect.width > 0 &&
                        rect.height > 0 &&
                        style.display !== 'none' &&
                        style.visibility !== 'hidden' &&
                        !input.disabled;
                });
            })();
            """,
            timeout);
    }

    private Task WaitForPaymentButtonReadyAsync(TimeSpan timeout)
    {
        return WaitForScriptTrueAsync(
            """
            (() => {
                const btn = document.querySelector('#iyz-payment-button') ||
                    Array.from(document.querySelectorAll('button, input[type="submit"]'))
                        .find(b => (b.textContent || b.value || '').trim().toLocaleLowerCase('tr-TR').includes('ödeme'));
                if (!btn) return false;

                const rect = btn.getBoundingClientRect();
                const style = window.getComputedStyle(btn);
                return rect.width > 0 &&
                    rect.height > 0 &&
                    style.display !== 'none' &&
                    style.visibility !== 'hidden' &&
                    !btn.disabled &&
                    btn.getAttribute('aria-disabled') !== 'true';
            })();
            """,
            timeout);
    }

    private Task<bool> WaitForPaymentFieldValueAsync(string selector, string expectedValue, TimeSpan timeout)
    {
        var selectorJson = JsonSerializer.Serialize(selector);
        var expectedJson = JsonSerializer.Serialize(NormalizeDigits(expectedValue));

        return WaitForScriptTrueOrTimeoutAsync(
            $$"""
            (() => {
                const input = document.querySelector({{selectorJson}});
                if (!input) return false;

                const actual = (input.value || '').trim();
                const actualDigits = actual.replace(/\D/g, '');
                const expectedDigits = {{expectedJson}};

                if (expectedDigits.length > 0) {
                    return actualDigits.endsWith(expectedDigits) || actualDigits === expectedDigits;
                }

                return actual.length > 0;
            })();
            """,
            timeout);
    }

    private static void ValidateSandboxCardInput(SandboxPaymentCardInput cardInput)
    {
        if (string.IsNullOrWhiteSpace(cardInput.CardHolderName))
            throw new InvalidOperationException("Kart sahibi adı boş.");

        if (NormalizeDigits(cardInput.CardNumber).Length < 15)
            throw new InvalidOperationException("Kart numarası geçersiz.");

        if (NormalizeDigits(cardInput.ExpiryMonth).Length != 2 || NormalizeDigits(cardInput.ExpiryYear).Length != 2)
            throw new InvalidOperationException("Son kullanma tarihi MM/YY formatında olmalı.");

        var cvcLength = NormalizeDigits(cardInput.Cvc).Length;
        if (cvcLength is < 3 or > 4)
            throw new InvalidOperationException("CVC geçersiz.");
    }

    private static string NormalizeDigits(string value)
    {
        return new string((value ?? string.Empty).Where(char.IsDigit).ToArray());
    }
}

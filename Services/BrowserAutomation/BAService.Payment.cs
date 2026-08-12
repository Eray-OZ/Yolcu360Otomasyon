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

        Report("Gömülü tarayıcıda iyzico ödeme sayfası açılıyor...");
        await NavigateAsync(paymentPageUrl);
        await WaitForDocumentReadyAsync();
        await EnsureJavaScriptHelpersAsync();

        Report("iyzico ödeme formu bekleniyor...");
        await WaitForScriptTrueAsync(
            """
            (() => {
                const selectors = ['#ccname', '#ccnumber', '#ccexp', '#cccvc'];
                return selectors.every(selector => !!document.querySelector(selector));
            })();
            """,
            TimeSpan.FromSeconds(30));

        // Ensure credit card tab is selected
        var creditCardTabClicked = await EvaluateBooleanScriptAsync(
            """
            (() => {
                const tab = document.querySelector('#iyz-tab-credit-card');
                if (!tab) return false;

                tab.click();
                return true;
            })();
            """);

        if (!creditCardTabClicked)
            throw new InvalidOperationException("Gömülü tarayıcıda iyzico kredi kartı sekmesi bulunamadı.");

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
                const button = document.querySelector('#iyz-payment-button');
                if (!button) return false;

                button.scrollIntoView({ block: 'center', inline: 'nearest' });
                button.click();

                return true;
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
                    return !!window.__ba?.isVisible(input) &&
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
                const button = document.querySelector('#iyz-payment-button');
                return !!window.__ba?.isVisible(button) &&
                    !button.disabled &&
                    button.getAttribute('aria-disabled') !== 'true';
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

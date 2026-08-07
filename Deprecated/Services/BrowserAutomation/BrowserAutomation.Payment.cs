using System.Text.Json;
using PuppeteerSharp;
using PuppeteerSharp.Input;
using Yolcu360Otomasyon.Models;

namespace Yolcu360Otomasyon.Services;

public sealed partial class BrowserAutomationService
{
    public async Task CompleteIyzicoSandboxPaymentAsync(string paymentPageUrl, SandboxPaymentCardInput cardInput)
    {
        var page = GetPage();

        if (string.IsNullOrWhiteSpace(paymentPageUrl))
            throw new InvalidOperationException("Ödeme sayfası adresi boş.");

        ValidateSandboxCardInput(cardInput);

        Report("iyzico ödeme sayfası açılıyor...");
        await page.GoToAsync(paymentPageUrl, WaitUntilNavigation.Networkidle2);
        await WaitAsync(1_500);

        await page.WaitForSelectorAsync("#ccname", new WaitForSelectorOptions
        {
            Visible = true,
            Timeout = 30_000
        });

        await EnsureCreditCardTabSelectedAsync();

        Report("Kart sahibi yazılıyor...");
        await TypeIntoPaymentFieldAsync("#ccname", cardInput.CardHolderName, 110);

        Report("Kart numarası yazılıyor...");
        await TypeIntoPaymentFieldAsync("#ccnumber", NormalizeDigits(cardInput.CardNumber), 95);

        Report("Son kullanma tarihi yazılıyor...");
        await TypeIntoPaymentFieldAsync("#ccexp", cardInput.ExpiryValue, 120);

        Report("CVC yazılıyor...");
        await TypeIntoPaymentFieldAsync("#cccvc", NormalizeDigits(cardInput.Cvc), 110);

        await WaitAsync(1_250);

        Report("Ödeme onay butonuna basılıyor...");
        var paymentClicked = await ClickElementHumanLikeAsync("#iyz-payment-button");
        if (!paymentClicked)
            throw new InvalidOperationException("iyzico ödeme butonu bulunamadı.");

        await WaitAsync(2_000);
    }

    private async Task EnsureCreditCardTabSelectedAsync()
    {
        var page = GetPage();
        var selected = await page.EvaluateExpressionAsync<bool>(
            """
            (() => {
                const tab = document.querySelector('#iyz-tab-credit-card');
                if (!tab) return false;
                return tab.classList.contains('selected') ||
                    tab.getAttribute('aria-selected') === 'true' ||
                    /selected|active/i.test(tab.className);
            })();
            """);

        if (!selected)
        {
            await ClickElementHumanLikeAsync("#iyz-tab-credit-card");
            await WaitAsync(700);
        }
    }

    private async Task TypeIntoPaymentFieldAsync(string selector, string value, int keyDelay)
    {
        var page = GetPage();

        await page.WaitForSelectorAsync(selector, new WaitForSelectorOptions
        {
            Visible = true,
            Timeout = 20_000
        });

        var clicked = await ClickElementHumanLikeAsync(selector);
        if (!clicked)
            throw new InvalidOperationException($"Ödeme alanı bulunamadı: {selector}");

        await WaitAsync(180);
        await page.FocusAsync(selector);
        await WaitAsync(120);

        await page.Keyboard.DownAsync("Meta");
        await page.Keyboard.PressAsync("A");
        await page.Keyboard.UpAsync("Meta");
        await WaitAsync(80);
        await page.Keyboard.PressAsync("Backspace");
        await WaitAsync(140);
        await page.Keyboard.TypeAsync(value, new TypeOptions { Delay = keyDelay });
        await WaitAsync(220);

        var selectorJson = JsonSerializer.Serialize(selector);
        await page.EvaluateExpressionAsync($$"""
            (() => {
                const el = document.querySelector({{selectorJson}});
                if (!el) return;
                el.dispatchEvent(new Event('input', { bubbles: true }));
                el.dispatchEvent(new Event('change', { bubbles: true }));
                el.dispatchEvent(new Event('blur', { bubbles: true }));
            })();
            """);
    }

    private async Task<bool> ClickElementHumanLikeAsync(string selector)
    {
        var page = GetPage();
        var point = await GetElementCenterPointAsync(selector);

        if (!point.Found || !point.Enabled)
            return false;

        await page.Mouse.MoveAsync(point.X - 24, point.Y - 7);
        await WaitAsync(110);
        await page.Mouse.MoveAsync(point.X - 8, point.Y - 2);
        await WaitAsync(90);
        await page.Mouse.MoveAsync(point.X, point.Y);
        await WaitAsync(150);
        await page.Mouse.ClickAsync(point.X, point.Y);
        await WaitAsync(250);
        return true;
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

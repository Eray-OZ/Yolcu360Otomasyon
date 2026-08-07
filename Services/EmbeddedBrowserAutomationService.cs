using Avalonia.Controls;
using Avalonia.Threading;
using System.Text.Json;
using Yolcu360Otomasyon.Models;

namespace Yolcu360Otomasyon.Services;

public sealed class EmbeddedBrowserAutomationService
{
    private const string Yolcu360HomeUrl = "https://www.yolcu360.com/";
    private const string PickupLocationInputSelector = "#inputPickUpLocation";
    private const string LocationSuggestionSelector = ".search-autocomplete__item, .search-autocomplete-mobile__item, .search-autocomplete .location-item, .location-item";
    private const string DateTimeGroupSelector = "[modaltitle='Alış ve Bırakış Tarihi']";
    private const string DatePickerSelector = ".dp__main.dp__theme_light";
    private readonly NativeWebView _browser;

    public event Action<string>? ProgressChanged;

    public EmbeddedBrowserAutomationService(NativeWebView browser)
    {
        _browser = browser;
    }

    public async Task NavigateAsync(string url, TimeSpan? timeout = null)
    {
        var target = new Uri(url);
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnNavigationCompleted(object? sender, WebViewNavigationCompletedEventArgs args)
        {
            Report($"Gömülü tarayıcı yükleme tamamlandı: {args.Request}");
            completion.TrySetResult(args.IsSuccess);
        }

        _browser.NavigationCompleted += OnNavigationCompleted;

        try
        {
            Report($"Gömülü tarayıcı gidiyor: {url}");
            await Dispatcher.UIThread.InvokeAsync(() => _browser.Navigate(target), DispatcherPriority.Render);

            using var timeoutCts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(45));
            await using var registration = timeoutCts.Token.Register(() => completion.TrySetCanceled(timeoutCts.Token));

            var succeeded = await completion.Task;
            if (!succeeded)
                throw new InvalidOperationException($"Sayfa yüklenemedi: {url}");
        }
        finally
        {
            _browser.NavigationCompleted -= OnNavigationCompleted;
        }
    }

    public Task<string?> EvaluateScriptAsync(string script)
    {
        return Dispatcher.UIThread.InvokeAsync(() => _browser.InvokeScript(script));
    }

    public async Task<string> GetTitleAsync()
    {
        return await EvaluateScriptAsync("document.title") ?? string.Empty;
    }

    public async Task OpenYolcu360HomeAsync()
    {
        Report("Yolcu360 ana sayfası açılıyor...");
        await NavigateAsync(Yolcu360HomeUrl);
        Report("Sayfanın hazır olması bekleniyor...");
        await WaitForDocumentReadyAsync();
        Report("Başlangıç popup'ı bekleniyor...");
        await Task.Delay(2_500);
        var popupClosed = await CloseInitialPopupAsync();
        Report(popupClosed ? "Başlangıç popup'ı kapatıldı." : "Başlangıç popup'ı görünmedi.");
    }

    public async Task WaitForDocumentReadyAsync(TimeSpan? timeout = null)
    {
        var deadline = DateTimeOffset.UtcNow + (timeout ?? TimeSpan.FromSeconds(20));

        while (DateTimeOffset.UtcNow < deadline)
        {
            var readyState = await EvaluateScriptAsync("document.readyState");
            if (string.Equals(readyState?.Trim('"'), "complete", StringComparison.OrdinalIgnoreCase))
                return;

            await Task.Delay(250);
        }

        throw new TimeoutException("Gömülü tarayıcı sayfa hazır durumuna geçmedi.");
    }

    public async Task<bool> CloseInitialPopupAsync()
    {
        var result = await EvaluateScriptAsync(
            """
            (() => {
                const closeButton = document.querySelector('.gs_trigger_discount_popup_close_container');
                if (!closeButton) return false;

                const rect = closeButton.getBoundingClientRect();
                const style = window.getComputedStyle(closeButton);
                const visible = rect.width > 0 &&
                    rect.height > 0 &&
                    style.visibility !== 'hidden' &&
                    style.display !== 'none';

                if (!visible) return false;

                closeButton.click();
                return true;
            })();
            """);

        return IsScriptTrue(result);
    }

    public async Task LoginWithPhoneAsync(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            throw new InvalidOperationException("Telefon numarası boş bırakılamaz.");

        Report("Gömülü tarayıcıda Yolcu360 login sayfası açılıyor...");
        await NavigateAsync("https://www.yolcu360.com/login?redirect=%2F");
        await WaitForDocumentReadyAsync();
        await Task.Delay(Random.Shared.Next(2000, 3000));
        await CloseInitialPopupAsync();

        Report("Telefon numarası inputu bekleniyor...");
        await WaitForScriptTrueAsync(
            """
            (() => !!document.querySelector('#phn-input') || !!document.querySelector('input[type="tel"]'))();
            """,
            TimeSpan.FromSeconds(20));

        var normalizedPhone = NormalizePhoneNumber(phoneNumber);
        Report($"Telefon numarası insansı davranışla yazılıyor: {normalizedPhone}");

        await Task.Delay(550);

        // Focus and clear input
        await EvaluateScriptAsync(
            """
            (() => {
                const input = document.querySelector('#phn-input') || document.querySelector('input[type="tel"]');
                if (!input) return false;
                input.scrollIntoView({ block: 'center', inline: 'nearest' });
                input.focus();
                input.value = '';
                input.dispatchEvent(new Event('input', { bubbles: true }));
                return true;
            })();
            """);

        await Task.Delay(220);

        // Type phone number character by character with human pauses
        var phoneChunks = SplitPhoneNumber(normalizedPhone);
        foreach (var chunk in phoneChunks)
        {
            foreach (var ch in chunk)
            {
                var charJson = JsonSerializer.Serialize(ch.ToString());
                await EvaluateScriptAsync(
                    $$"""
                    (() => {
                        const input = document.querySelector('#phn-input') || document.querySelector('input[type="tel"]');
                        if (!input) return false;
                        const char = {{charJson}};
                        input.dispatchEvent(new KeyboardEvent('keydown', { bubbles: true, key: char }));
                        input.value += char;
                        input.dispatchEvent(new InputEvent('input', { bubbles: true, inputType: 'insertText', data: char }));
                        input.dispatchEvent(new KeyboardEvent('keyup', { bubbles: true, key: char }));
                        return true;
                    })();
                    """);
                await Task.Delay(Random.Shared.Next(135, 190));
            }
            await Task.Delay(Random.Shared.Next(200, 350));
        }

        // Trigger change & blur
        await EvaluateScriptAsync(
            """
            (() => {
                const input = document.querySelector('#phn-input') || document.querySelector('input[type="tel"]');
                if (!input) return;
                input.dispatchEvent(new Event('change', { bubbles: true }));
                input.dispatchEvent(new Event('blur', { bubbles: true }));
            })();
            """);

        // Wait 2.8 seconds as in BrowserAutomation.Login.cs
        Report("Telefon numarası girildi, 2.8 saniye bekleniyor (reCAPTCHA / insansı duraklama)...");
        await Task.Delay(Random.Shared.Next(2600, 3000));

        Report("'Devam Et' butonuna insansı şekilde tıklanıyor...");
        var continueClicked = await EvaluateScriptAsync(
            """
            (() => {
                const btn = Array.from(document.querySelectorAll('button, input[type="submit"]'))
                    .find(b => (b.textContent || b.value || '').trim().toLowerCase().includes('devam'));
                if (!btn) return false;

                btn.scrollIntoView({ block: 'center', inline: 'nearest' });
                const rect = btn.getBoundingClientRect();
                const x = rect.left + rect.width / 2;
                const y = rect.top + rect.height / 2;
                const opts = { bubbles: true, cancelable: true, view: window, clientX: x, clientY: y };

                if (typeof PointerEvent === 'function') {
                    btn.dispatchEvent(new PointerEvent('pointerdown', { ...opts, pointerId: 1, pointerType: 'mouse', isPrimary: true, buttons: 1 }));
                    btn.dispatchEvent(new PointerEvent('pointerup', { ...opts, pointerId: 1, pointerType: 'mouse', isPrimary: true }));
                }
                btn.dispatchEvent(new MouseEvent('mousedown', { ...opts, buttons: 1 }));
                btn.dispatchEvent(new MouseEvent('mouseup', opts));
                btn.click();
                return true;
            })();
            """);

        if (!IsScriptTrue(continueClicked))
            throw new InvalidOperationException("Gömülü tarayıcıda 'Devam Et' butonu tıklanamadı.");

        Report("SMS doğrulama ekranı bekleniyor...");
        await WaitForScriptTrueAsync(
            """
            (() => {
                const text = (document.body.innerText || '').toLocaleLowerCase('tr-TR');
                const inputs = document.querySelectorAll('input');
                return text.includes('doğrulama') || text.includes('sms') || inputs.length > 0;
            })();
            """,
            TimeSpan.FromSeconds(25));
    }

    public async Task FillSmsVerificationCodeAsync(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new InvalidOperationException("SMS doğrulama kodu boş olamaz.");

        Report($"Gömülü tarayıcıda insansı davranışla SMS kodu yazılıyor: {code}");
        await Task.Delay(Random.Shared.Next(1000, 1800));

        var codeJson = JsonSerializer.Serialize(code.Trim());

        // First, attempt digit-by-digit entry with React native setter
        for (int i = 0; i < code.Length; i++)
        {
            var digit = code[i];
            var index = i;
            var digitJson = JsonSerializer.Serialize(digit.ToString());

            var digitSet = await EvaluateScriptAsync(
                $$"""
                (() => {
                    const digit = {{digitJson}};
                    const idx = {{index}};

                    const normalize = value => (value || '').toLocaleLowerCase('tr-TR');
                    const visible = el => {
                        const rect = el.getBoundingClientRect();
                        const style = window.getComputedStyle(el);
                        return rect.width > 0 && rect.height > 0 && style.display !== 'none' && style.visibility !== 'hidden';
                    };

                    const setValue = (el, val) => {
                        const proto = el instanceof HTMLInputElement || el instanceof HTMLTextAreaElement ? Object.getPrototypeOf(el) : null;
                        const desc = proto ? Object.getOwnPropertyDescriptor(proto, 'value') : null;
                        if (desc && desc.set) {
                            desc.set.call(el, val);
                        } else if ('value' in el) {
                            el.value = val;
                        } else {
                            el.textContent = val;
                        }
                    };

                    const dispatchEvents = el => {
                        el.focus();
                        el.dispatchEvent(new KeyboardEvent('keydown', { bubbles: true, key: digit }));
                        el.dispatchEvent(new KeyboardEvent('keypress', { bubbles: true, key: digit }));
                        el.dispatchEvent(new InputEvent('input', { bubbles: true, inputType: 'insertText', data: digit }));
                        el.dispatchEvent(new Event('input', { bubbles: true }));
                        el.dispatchEvent(new Event('change', { bubbles: true }));
                        el.dispatchEvent(new KeyboardEvent('keyup', { bubbles: true, key: digit }));
                    };

                    const allInputs = Array.from(document.querySelectorAll('input, [contenteditable="true"]')).filter(visible);
                    const otpInputs = allInputs.filter(input => {
                        const attrs = normalize(`${input.id} ${input.name} ${input.placeholder} ${input.autocomplete} ${input.inputMode} ${input.type} ${input.className} ${input.getAttribute?.('aria-label') || ''}`);
                        return attrs.includes('otp') || attrs.includes('code') || attrs.includes('kod') || attrs.includes('pin') || attrs.includes('verify') || attrs.includes('dogrulama') || attrs.includes('sms') || input.maxLength === 1;
                    });

                    const singleCharInputs = otpInputs.filter(input => input.maxLength === 1);
                    if (singleCharInputs.length > idx) {
                        const input = singleCharInputs[idx];
                        setValue(input, digit);
                        dispatchEvents(input);
                        return true;
                    }

                    if (otpInputs.length > 0) {
                        const input = otpInputs[0];
                        if (idx === 0) setValue(input, '');
                        setValue(input, input.value + digit);
                        dispatchEvents(input);
                        return true;
                    }

                    if (allInputs.length > 0) {
                        const input = allInputs[0];
                        if (idx === 0) setValue(input, '');
                        setValue(input, input.value + digit);
                        dispatchEvents(input);
                        return true;
                    }

                    return false;
                })();
                """);

            if (!IsScriptTrue(digitSet))
            {
                Report($"Gömülü tarayıcıda {index + 1}. hane ({digit}) için kutu bulunamadı, fallback deneniyor...");
            }

            await Task.Delay(Random.Shared.Next(280, 420));
        }

        // Entire code fallback injection via React setter if single digit entry missed any fields
        var fullFilled = await EvaluateScriptAsync(
            $$"""
            (() => {
                const code = {{codeJson}};
                const normalize = value => (value || '').toLocaleLowerCase('tr-TR');
                const visible = el => {
                    const rect = el.getBoundingClientRect();
                    const style = window.getComputedStyle(el);
                    return rect.width > 0 && rect.height > 0 && style.display !== 'none' && style.visibility !== 'hidden';
                };

                const setValue = (el, val) => {
                    const proto = el instanceof HTMLInputElement || el instanceof HTMLTextAreaElement ? Object.getPrototypeOf(el) : null;
                    const desc = proto ? Object.getOwnPropertyDescriptor(proto, 'value') : null;
                    if (desc && desc.set) {
                        desc.set.call(el, val);
                    } else if ('value' in el) {
                        el.value = val;
                    } else {
                        el.textContent = val;
                    }
                };

                const dispatchEvents = el => {
                    el.dispatchEvent(new Event('input', { bubbles: true }));
                    el.dispatchEvent(new Event('change', { bubbles: true }));
                    el.dispatchEvent(new Event('blur', { bubbles: true }));
                };

                const allInputs = Array.from(document.querySelectorAll('input, [contenteditable="true"]')).filter(visible);
                const singleCharInputs = allInputs.filter(input => input.maxLength === 1);
                if (singleCharInputs.length >= code.length) {
                    singleCharInputs.slice(0, code.length).forEach((input, idx) => {
                        setValue(input, code[idx]);
                        dispatchEvents(input);
                    });
                    return true;
                }

                if (allInputs.length > 0) {
                    setValue(allInputs[0], code);
                    dispatchEvents(allInputs[0]);
                    return true;
                }

                return false;
            })();
            """);

        Report(IsScriptTrue(fullFilled)
            ? $"SMS kodu ({code}) gömülü tarayıcıdaki kutulara insansı şekilde yazıldı."
            : $"UYARI: SMS kodu ({code}) için uygun HTML kutusu tespit edilemedi.");

        // Wait 4.5 seconds after filling SMS code before clicking approve
        Report("SMS kodu girildi, doğrulama butonuna basmadan önce 4.5 saniye bekleniyor (reCAPTCHA / sayfa onay süresi)...");
        await Task.Delay(Random.Shared.Next(4200, 5500));

        Report("SMS doğrulama butonu tıklanıyor...");
        await EvaluateScriptAsync(
            """
            (() => {
                const applyBtn = document.querySelector('button[data-cms-key="button_apply"]');
                if (applyBtn) {
                    applyBtn.scrollIntoView({ block: 'center', inline: 'nearest' });
                    const rect = applyBtn.getBoundingClientRect();
                    const x = rect.left + rect.width / 2;
                    const y = rect.top + rect.height / 2;
                    const opts = { bubbles: true, cancelable: true, view: window, clientX: x, clientY: y };
                    applyBtn.dispatchEvent(new MouseEvent('mousedown', { ...opts, buttons: 1 }));
                    applyBtn.dispatchEvent(new MouseEvent('mouseup', opts));
                    applyBtn.click();
                    return true;
                }

                const btn = Array.from(document.querySelectorAll('button, input[type="submit"]'))
                    .find(b => {
                        const txt = (b.textContent || b.value || '').trim().toLowerCase();
                        return (txt.includes('doğrula') || txt.includes('onayla') || txt.includes('devam') || txt.includes('giriş'));
                    });
                if (btn) {
                    btn.scrollIntoView({ block: 'center', inline: 'nearest' });
                    const rect = btn.getBoundingClientRect();
                    const x = rect.left + rect.width / 2;
                    const y = rect.top + rect.height / 2;
                    const opts = { bubbles: true, cancelable: true, view: window, clientX: x, clientY: y };
                    btn.dispatchEvent(new MouseEvent('mousedown', { ...opts, buttons: 1 }));
                    btn.dispatchEvent(new MouseEvent('mouseup', opts));
                    btn.click();
                    return true;
                }
                return false;
            })();
            """);
    }

    public async Task WaitForLoginCompletedAsync(TimeSpan? timeout = null)
    {
        var deadline = DateTimeOffset.UtcNow + (timeout ?? TimeSpan.FromSeconds(30));
        Report("Giriş işleminin tamamlanması bekleniyor...");

        while (DateTimeOffset.UtcNow < deadline)
        {
            var isCompleted = await EvaluateScriptAsync(
                """
                (() => {
                    const url = window.location.href;
                    const text = (document.body.innerText || '').toLocaleLowerCase('tr-TR');
                    return !url.includes('login') || text.includes('hesabım') || text.includes('profil') || text.includes('hoş geldin');
                })();
                """);

            if (IsScriptTrue(isCompleted))
            {
                Report("Giriş başarıyla tamamlandı.");
                return;
            }

            await Task.Delay(500);
        }

        Report("Giriş tamamlanma kontrolü zaman aşımına uğradı, ancak devam ediliyor.");
    }

    public async Task CompleteIyzicoSandboxPaymentAsync(string paymentPageUrl, SandboxPaymentCardInput cardInput)
    {
        if (string.IsNullOrWhiteSpace(paymentPageUrl))
            throw new InvalidOperationException("iyzico ödeme sayfası adresi boş.");

        ValidateSandboxCardInput(cardInput);

        Report("Gömülü tarayıcıda iyzico ödeme sayfası açılıyor...");
        await NavigateAsync(paymentPageUrl);
        await WaitForDocumentReadyAsync();
        await Task.Delay(2000);

        Report("iyzico ödeme formu bekleniyor...");
        await WaitForScriptTrueAsync(
            """
            (() => !!document.querySelector('#ccname') || !!document.querySelector('#ccnumber') || !!document.querySelector('input[name*="card"]'))();
            """,
            TimeSpan.FromSeconds(30));

        // Ensure credit card tab is selected
        await EvaluateScriptAsync(
            """
            (() => {
                const tab = document.querySelector('#iyz-tab-credit-card');
                if (tab) tab.click();
                return true;
            })();
            """);

        await Task.Delay(600);

        Report("Gömülü tarayıcıda Kart Sahibi yazılıyor...");
        await TypeIntoPaymentFieldAsync("#ccname", cardInput.CardHolderName);

        Report("Gömülü tarayıcıda Kart Numarası yazılıyor...");
        await TypeIntoPaymentFieldAsync("#ccnumber", NormalizeDigits(cardInput.CardNumber));

        Report("Gömülü tarayıcıda Son Kullanma Tarihi yazılıyor...");
        await TypeIntoPaymentFieldAsync("#ccexp", cardInput.ExpiryValue);

        Report("Gömülü tarayıcıda CVC yazılıyor...");
        await TypeIntoPaymentFieldAsync("#cccvc", NormalizeDigits(cardInput.Cvc));

        await Task.Delay(1250);

        Report("iyzico ödeme onay butonuna tıklanıyor...");
        var paymentClicked = await EvaluateScriptAsync(
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

        if (!IsScriptTrue(paymentClicked))
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

        await Task.Delay(Random.Shared.Next(300, 500));
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

    public async Task ApplyResultFiltersAsync(SearchFilter filter)
    {
        if (filter is null) return;

        var hasTransmission = !string.IsNullOrWhiteSpace(filter.TransmissionType);
        var hasFuel = !string.IsNullOrWhiteSpace(filter.FuelType);

        if (!hasTransmission && !hasFuel) return;

        Report($"Gömülü tarayıcıda filtreler uygulanıyor (Vites: {filter.TransmissionType}, Yakıt: {filter.FuelType})...");
        await Task.Delay(1200);

        if (hasTransmission)
        {
            var transmissionNorm = filter.TransmissionType.Trim().ToLowerInvariant();
            var targetTexts = transmissionNorm switch
            {
                "otomatik" or "automatic" => new[] { "otomatik" },
                "manuel" or "manual" => new[] { "manuel" },
                _ => Array.Empty<string>()
            };

            if (targetTexts.Length > 0)
            {
                await ClickFilterOptionAsync("Vites filtresi", "filter-transmission", targetTexts);
                await Task.Delay(1000);
            }
        }

        if (hasFuel)
        {
            var fuelNorm = filter.FuelType.Trim().ToLowerInvariant();
            var targetTexts = fuelNorm switch
            {
                "dizel" or "diesel" => new[] { "dizel", "benzin/dizel" },
                "benzin" or "gasoline" => new[] { "benzin", "benzin/dizel" },
                _ => Array.Empty<string>()
            };

            if (targetTexts.Length > 0)
            {
                await ClickFilterOptionAsync("Yakıt filtresi", "filter-fuel", targetTexts);
                await Task.Delay(1000);
            }
        }

        Report("Filtreler uygulandı, sonuçların yenilenmesi bekleniyor...");
        await Task.Delay(1500);
        await WaitForSearchResultsAsync();
    }

    private async Task<bool> ClickFilterOptionAsync(string filterName, string filterPrefix, string[] targetTexts)
    {
        var targetTextsJson = JsonSerializer.Serialize(targetTexts);
        var filterPrefixJson = JsonSerializer.Serialize(filterPrefix);

        Report($"{filterName} aranıyor ({string.Join(", ", targetTexts)})...");

        var scriptResult = await EvaluateScriptAsync(
            $$"""
            (() => {
                const targets = {{targetTextsJson}};
                const prefix = {{filterPrefixJson}};

                const normalize = value => (value || '')
                    .toLocaleLowerCase('tr-TR')
                    .replace(/\s+/g, ' ')
                    .trim();

                const visible = el => {
                    const rect = el.getBoundingClientRect();
                    const style = window.getComputedStyle(el);
                    return rect.width > 0 && rect.height > 0 && style.display !== 'none' && style.visibility !== 'hidden';
                };

                const normalizedTargets = targets.map(normalize);

                let labels = Array.from(document.querySelectorAll(`label[name^="${prefix}."], input[name^="${prefix}."]`)).filter(visible);

                if (labels.length === 0) {
                    labels = Array.from(document.querySelectorAll('label, input[type="checkbox"], input[type="radio"]')).filter(visible);
                }

                const score = text => {
                    if (normalizedTargets.includes(text)) return 0;
                    if (normalizedTargets.some(target => text.startsWith(target + ' '))) return 1;
                    if (normalizedTargets.some(target => text.includes(target))) return 2;
                    return 3;
                };

                const candidates = labels
                    .map(el => {
                        const text = normalize(el.textContent || el.value || el.getAttribute('aria-label') || '');
                        return { el, text };
                    })
                    .filter(item => item.text.length > 0)
                    .sort((a, b) => score(a.text) - score(b.text));

                const match = candidates.find(item => score(item.text) < 3);
                if (!match) return false;

                const targetEl = match.el;
                targetEl.scrollIntoView({ block: 'center', inline: 'nearest' });

                ['pointerdown', 'mousedown', 'pointerup', 'mouseup', 'click'].forEach(type => {
                    targetEl.dispatchEvent(new MouseEvent(type, { bubbles: true, cancelable: true, view: window }));
                });
                targetEl.click();

                const checkbox = targetEl.querySelector?.('input[type="checkbox"], input[type="radio"]') || (targetEl.tagName === 'INPUT' ? targetEl : null);
                if (checkbox && !checkbox.checked) {
                    checkbox.click();
                    checkbox.dispatchEvent(new Event('change', { bubbles: true }));
                }

                return true;
            })();
            """);

        var success = IsScriptTrue(scriptResult);
        Report(success
            ? $"{filterName} başarıyla uygulandı."
            : $"UYARI: {filterName} bulunamadı veya uygulanamadı.");

        return success;
    }

    public async Task SaveSessionAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return;

        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var cookiesRaw = await EvaluateScriptAsync("document.cookie");
            var cookies = (cookiesRaw ?? string.Empty).Trim().Trim('"');

            var localStorageJson = await EvaluateScriptAsync(
                """
                (() => {
                    const result = {};
                    try {
                        for (let i = 0; i < localStorage.length; i++) {
                            const key = localStorage.key(i);
                            if (key) result[key] = localStorage.getItem(key);
                        }
                    } catch {}
                    return JSON.stringify(result);
                })();
                """);

            var sessionStorageJson = await EvaluateScriptAsync(
                """
                (() => {
                    const result = {};
                    try {
                        for (let i = 0; i < sessionStorage.length; i++) {
                            const key = sessionStorage.key(i);
                            if (key) result[key] = sessionStorage.getItem(key);
                        }
                    } catch {}
                    return JSON.stringify(result);
                })();
                """);

            var localStorage = DeserializeStorage(localStorageJson);
            var sessionStorage = DeserializeStorage(sessionStorageJson);

            var currentUrlRaw = await EvaluateScriptAsync("window.location.href");
            var currentUrl = (currentUrlRaw ?? string.Empty).Trim().Trim('"');

            var state = new EmbeddedSessionState
            {
                SavedAt = DateTimeOffset.UtcNow,
                CurrentUrl = currentUrl,
                Cookies = cookies,
                LocalStorage = localStorage,
                SessionStorage = sessionStorage
            };

            var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, json);
            Report($"Oturum gömülü tarayıcıdan dosyaya kaydedildi: {filePath}");
        }
        catch (Exception ex)
        {
            Report($"Oturum kaydetme hatası: {ex.Message}");
        }
    }

    public async Task<bool> RestoreSessionAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return false;

        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            var state = JsonSerializer.Deserialize<EmbeddedSessionState>(json);
            if (state is null) return false;

            Report($"Kaydedilmiş oturum gömülü tarayıcıya yükleniyor ({filePath})...");

            if (!string.IsNullOrWhiteSpace(state.Cookies))
            {
                var cookieParts = state.Cookies.Split(';', StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in cookieParts)
                {
                    var partJson = JsonSerializer.Serialize(part.Trim() + "; path=/; domain=.yolcu360.com");
                    await EvaluateScriptAsync($"document.cookie = {partJson};");
                }
            }

            if (state.LocalStorage.Count > 0)
            {
                var localJson = JsonSerializer.Serialize(state.LocalStorage);
                await EvaluateScriptAsync(
                    $$"""
                    (() => {
                        const items = {{localJson}};
                        for (const key in items) {
                            if (Object.prototype.hasOwnProperty.call(items, key)) {
                                localStorage.setItem(key, items[key]);
                            }
                        }
                    })();
                    """);
            }

            if (state.SessionStorage.Count > 0)
            {
                var sessionJson = JsonSerializer.Serialize(state.SessionStorage);
                await EvaluateScriptAsync(
                    $$"""
                    (() => {
                        const items = {{sessionJson}};
                        for (const key in items) {
                            if (Object.prototype.hasOwnProperty.call(items, key)) {
                                sessionStorage.setItem(key, items[key]);
                            }
                        }
                    })();
                    """);
            }

            Report("Kaydedilmiş oturum gömülü tarayıcıya başarıyla restore edildi.");
            return true;
        }
        catch (Exception ex)
        {
            Report($"Oturum yükleme hatası: {ex.Message}");
            return false;
        }
    }

    private static Dictionary<string, string?> DeserializeStorage(string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
            return new Dictionary<string, string?>();

        try
        {
            var unescaped = rawJson.Trim().Trim('"').Replace("\\\"", "\"");
            return JsonSerializer.Deserialize<Dictionary<string, string?>>(unescaped) ?? new Dictionary<string, string?>();
        }
        catch
        {
            return new Dictionary<string, string?>();
        }
    }

    public async Task FillPickupLocationAsync(string location)
    {
        if (string.IsNullOrWhiteSpace(location))
            throw new InvalidOperationException("Alış yeri boş bırakılamaz.");

        var locationJson = JsonSerializer.Serialize(location.Trim());
        var pickupLocationInputSelectorJson = JsonSerializer.Serialize(PickupLocationInputSelector);
        var locationSuggestionSelectorJson = JsonSerializer.Serialize(LocationSuggestionSelector);
        var diagnostic = await GetSearchDomDiagnosticAsync();
        Report($"Gömülü DOM: {diagnostic}");

        Report("Alış yeri inputu bekleniyor...");
        await WaitForScriptTrueAsync(
            $$"""
            (() => !!document.querySelector({{pickupLocationInputSelectorJson}}))();
            """,
            TimeSpan.FromSeconds(20));

        Report($"Alış yeri yazılıyor: {location}");
        await EvaluateScriptAsync(
            $$"""
            (() => {
                const input = document.querySelector({{pickupLocationInputSelectorJson}});
                const text = {{locationJson}};
                input.focus();
                input.value = '';
                input.dispatchEvent(new InputEvent('input', { bubbles: true, inputType: 'deleteContentBackward', data: null }));

                for (const char of text) {
                    input.dispatchEvent(new KeyboardEvent('keydown', { bubbles: true, key: char }));
                    input.value += char;
                    input.dispatchEvent(new InputEvent('input', { bubbles: true, inputType: 'insertText', data: char }));
                    input.dispatchEvent(new KeyboardEvent('keyup', { bubbles: true, key: char }));
                }

                input.dispatchEvent(new Event('change', { bubbles: true }));
                return true;
            })();
            """);

        Report("Alış yeri önerileri bekleniyor...");
        await WaitForLocationSuggestionsAsync(LocationSuggestionSelector, TimeSpan.FromSeconds(12));

        var selectionApplied = false;
        for (var attempt = 1; attempt <= 3 && !selectionApplied; attempt++)
        {
            Report($"Alış yeri önerisi seçiliyor. Deneme: {attempt}");
            var selected = await EvaluateScriptAsync(
                $$"""
                (() => {
                    const input = document.querySelector({{pickupLocationInputSelectorJson}});
                    const targetText = {{locationJson}};
                    const normalize = value => (value || '')
                        .toLocaleLowerCase('tr-TR')
                        .replace(/\s+/g, ' ')
                        .trim();
                    const compact = value => normalize(value).replace(/\s/g, '');
                    const target = normalize(targetText);
                    const visible = item => {
                        const rect = item.getBoundingClientRect();
                        const style = getComputedStyle(item);
                        return rect.width > 0 &&
                            rect.height > 0 &&
                            style.display !== 'none' &&
                            style.visibility !== 'hidden';
                    };
                    const getMainText = item => normalize(
                        item.querySelector('strong, .search-autocomplete__item__text-wrapper span:first-child, .search-autocomplete-mobile__item__text-wrapper span:first-child, div > div:first-child')?.textContent || ''
                    );
                    const getScore = item => {
                        const fullText = normalize(item.textContent || '');
                        const mainText = getMainText(item);
                        const compactText = compact(item.textContent || '');
                        const hasAirportText =
                            fullText.includes('airport') ||
                            fullText.includes('havalimanı') ||
                            fullText.includes('sabiha') ||
                            fullText.includes('saw') ||
                            fullText.includes('ist)');

                        if (mainText === target) return 0;
                        if (compactText === compact(`${targetText} Türkiye`) || compactText === compact(`${targetText}, Türkiye`)) return 1;
                        if (fullText === target) return 2;
                        if (!hasAirportText && mainText.startsWith(target + ' ')) return 3;
                        if (!hasAirportText && fullText.startsWith(target)) return 4;
                        if (mainText.startsWith(target)) return 5;
                        if (fullText.startsWith(target)) return 6;
                        if (mainText.includes(target)) return 7;
                        if (fullText.includes(target)) return 8;
                        return 9;
                    };

                    const items = Array.from(document.querySelectorAll({{locationSuggestionSelectorJson}}))
                        .filter(item => visible(item) && (!input || (item !== input && !item.contains(input))));
                    const selected = items
                        .sort((a, b) => {
                            const score = getScore(a) - getScore(b);
                            if (score !== 0) return score;
                            const ar = a.getBoundingClientRect();
                            const br = b.getBoundingClientRect();
                            return ar.top === br.top ? ar.left - br.left : ar.top - br.top;
                        })[0];

                    if (!selected) return JSON.stringify({ clicked: false, reason: 'öneri bulunamadı', itemCount: items.length });

                    selected.scrollIntoView({ block: 'center', inline: 'nearest' });
                    const rect = selected.getBoundingClientRect();
                    const x = rect.left + rect.width / 2;
                    const y = rect.top + rect.height / 2;
                    const pointTarget = document.elementFromPoint(x, y);
                    const eventTarget = pointTarget?.closest?.({{locationSuggestionSelectorJson}}) || pointTarget || selected;
                    const eventOptions = { bubbles: true, cancelable: true, view: window, clientX: x, clientY: y };

                    const dispatchPointer = (target, type, buttons = 0) => {
                        if (!target) return;
                        if (typeof PointerEvent === 'function') {
                            target.dispatchEvent(new PointerEvent(type, { ...eventOptions, pointerId: 1, pointerType: 'mouse', isPrimary: true, buttons }));
                        }
                    };
                    const dispatchMouse = (target, type, buttons = 0) => {
                        if (!target) return;
                        target.dispatchEvent(new MouseEvent(type, { ...eventOptions, buttons }));
                    };

                    for (const target of [eventTarget, selected]) {
                        dispatchPointer(target, 'pointerover');
                        dispatchMouse(target, 'mouseover');
                        dispatchMouse(target, 'mousemove');
                        dispatchPointer(target, 'pointerdown', 1);
                        dispatchMouse(target, 'mousedown', 1);
                        dispatchPointer(target, 'pointerup');
                        dispatchMouse(target, 'mouseup');
                        dispatchMouse(target, 'click');
                    }

                    return JSON.stringify({
                        clicked: true,
                        selectedText: (selected.textContent || '').replace(/\s+/g, ' ').trim(),
                        pointTargetText: (pointTarget?.textContent || '').replace(/\s+/g, ' ').trim().slice(0, 120),
                        inputValue: input?.value || '',
                        remainingSuggestions: document.querySelectorAll({{locationSuggestionSelectorJson}}).length
                    });
                })();
                """);

            Report($"Alış yeri seçim sonucu: {selected}");
            await Task.Delay(700);
            selectionApplied = await IsPickupLocationSelectionAppliedAsync();
        }

        if (!selectionApplied)
            throw new InvalidOperationException("Alış yeri önerisi seçilemedi.");

        Report("Alış yeri önerisi seçildi.");
    }

    public async Task SelectDateRangeAsync(DateTime pickupDate, DateTime returnDate)
    {
        Report($"Alış ve Bırakış tarihleri seçiliyor: {pickupDate:dd.MM.yyyy} – {returnDate:dd.MM.yyyy}");

        Report("Tarih seçici açılıyor...");
        var opened = await OpenDatePickerAsync();
        if (!opened)
            throw new InvalidOperationException("Tarih seçici (datepicker) açılamadı.");

        Report("Tarih takvimi bekleniyor...");
        await WaitForDatePickerMenuAsync(TimeSpan.FromSeconds(10));

        Report($"Alış tarihi için ay kontrol ediliyor: {pickupDate:MMMM yyyy}");
        await NavigateToMonthAsync(pickupDate);
        await Task.Delay(300);

        Report($"Alış tarihi seçiliyor: {pickupDate:dd.MM.yyyy}");
        var pickupSelected = await ClickCalendarDayAsync(pickupDate);
        if (!pickupSelected)
            throw new InvalidOperationException($"Alış tarihi ({pickupDate:dd.MM.yyyy}) takvimde seçilemedi.");

        Report($"Alış tarihi seçildi: {pickupDate:dd.MM.yyyy}");
        await Task.Delay(400);

        if (returnDate.Year != pickupDate.Year || returnDate.Month != pickupDate.Month)
        {
            Report($"Bırakış tarihi için ay geziliyor: {returnDate:MMMM yyyy}");
            await NavigateToMonthAsync(returnDate);
            await Task.Delay(300);
        }

        Report($"Bırakış tarihi seçiliyor: {returnDate:dd.MM.yyyy}");
        var returnSelected = await ClickCalendarDayAsync(returnDate);
        if (!returnSelected)
            throw new InvalidOperationException($"Bırakış tarihi ({returnDate:dd.MM.yyyy}) takvimde seçilemedi.");

        Report($"Bırakış tarihi seçildi: {returnDate:dd.MM.yyyy}");
        await Task.Delay(400);

        await ConfirmDatePickerAsync();
        await Task.Delay(300);
    }

    private async Task<bool> OpenDatePickerAsync()
    {
        var datePickerSelectorJson = JsonSerializer.Serialize(DatePickerSelector);
        var dateTimeGroupSelectorJson = JsonSerializer.Serialize(DateTimeGroupSelector);

        var result = await EvaluateScriptAsync(
            $$"""
            (() => {
                const labelEl = Array.from(document.querySelectorAll('span, div, label, p'))
                    .find(el => {
                        const txt = (el.textContent || '').trim();
                        return txt === 'Alış Tarihi' || txt === 'Alış ve Bırakış Tarihi';
                    });
                const pickerFromLabel = labelEl?.closest('.dp__main, [modaltitle="Alış ve Bırakış Tarihi"], [modaltitlecmskey="pickup_and_dropoff_date"]');
                const pickerBySelector = document.querySelector({{datePickerSelectorJson}}) || document.querySelector({{dateTimeGroupSelectorJson}});

                const target = pickerFromLabel || pickerBySelector || labelEl;
                if (!target) return 'false';

                target.scrollIntoView({ block: 'center', inline: 'nearest' });
                const rect = target.getBoundingClientRect();
                const x = rect.left + rect.width / 2;
                const y = rect.top + rect.height / 2;

                const triggerEvents = (el) => {
                    if (!el) return;
                    const opts = { bubbles: true, cancelable: true, view: window, clientX: x, clientY: y };
                    if (typeof PointerEvent === 'function') {
                        el.dispatchEvent(new PointerEvent('pointerdown', { ...opts, pointerId: 1, pointerType: 'mouse', isPrimary: true, buttons: 1 }));
                        el.dispatchEvent(new PointerEvent('pointerup', { ...opts, pointerId: 1, pointerType: 'mouse', isPrimary: true }));
                    }
                    el.dispatchEvent(new MouseEvent('mousedown', { ...opts, buttons: 1 }));
                    el.dispatchEvent(new MouseEvent('mouseup', { ...opts }));
                    el.dispatchEvent(new MouseEvent('click', opts));
                    if (typeof el.click === 'function') el.click();
                };

                triggerEvents(target);
                const innerInput = target.querySelector('input, .dp__input, .dp__icon');
                if (innerInput && innerInput !== target) {
                    triggerEvents(innerInput);
                }

                return 'true';
            })();
            """);

        return IsScriptTrue(result);
    }

    private async Task WaitForDatePickerMenuAsync(TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            var menuVisible = await EvaluateScriptAsync(
                """
                (() => {
                    const menus = Array.from(document.querySelectorAll('.dp__menu, .dp__outer_menu_wrap, .dp__calendar'));
                    return menus.some(m => {
                        const rect = m.getBoundingClientRect();
                        const style = window.getComputedStyle(m);
                        return rect.width > 0 && rect.height > 0 && style.display !== 'none' && style.visibility !== 'hidden';
                    });
                })();
                """);

            if (IsScriptTrue(menuVisible))
                return;

            await Task.Delay(250);
        }

        throw new TimeoutException("Tarih seçici takvim menüsü (dp__menu) görünmedi.");
    }

    private async Task NavigateToMonthAsync(DateTime target)
    {
        for (var attempt = 0; attempt < 24; attempt++)
        {
            var headerText = await EvaluateScriptAsync(
                """
                (() => {
                    const menu = Array.from(document.querySelectorAll('.dp__menu, .dp__outer_menu_wrap'))
                        .find(m => {
                            const s = window.getComputedStyle(m);
                            const r = m.getBoundingClientRect();
                            return s.display !== 'none' && s.visibility !== 'hidden' && r.width > 0;
                        });
                    if (!menu) return '';
                    const headers = Array.from(menu.querySelectorAll('.dp__month_year_select, .dp__calendar_header_item, .dp__month_year_wrap, .dp__calendar_header'));
                    return headers.map(h => (h.textContent || '').trim()).join(' ');
                })();
                """);

            var currentText = (headerText ?? string.Empty).Trim('"');
            Report($"Takvim başlığı: '{currentText}' | Hedef: {target:MMMM yyyy}");

            if (IsTargetMonthVisible(currentText, target))
                return;

            var goBack = ShouldGoBack(currentText, target);
            var navSuccess = await ClickCalendarNavAsync(forward: !goBack);
            if (!navSuccess)
            {
                Report("Takvim yönlendirme butonuna tıklanamadı.");
                break;
            }

            await Task.Delay(300);
        }
    }

    private async Task<bool> ClickCalendarNavAsync(bool forward)
    {
        var forwardJson = JsonSerializer.Serialize(forward);
        var result = await EvaluateScriptAsync(
            $$"""
            (() => {
                const forward = {{forwardJson}};
                const next = document.querySelector("[data-dp-element='action-next'], .dp__next_btn, button[aria-label*='Next']");
                const prev = document.querySelector("[data-dp-element='action-prev'], .dp__prev_btn, button[aria-label*='Prev']");
                const navBtns = Array.from(document.querySelectorAll('.dp__nav_btn'));

                const btn = forward
                    ? (next || (navBtns.length > 1 ? navBtns[navBtns.length - 1] : navBtns[0]))
                    : (prev || navBtns[0]);

                if (!btn) return false;

                btn.click();
                return true;
            })();
            """);

        return IsScriptTrue(result);
    }

    private async Task<bool> ClickCalendarDayAsync(DateTime date)
    {
        var dayJson = JsonSerializer.Serialize(date.Day);
        var turkishMonths = new[]
        {
            "Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran",
            "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık"
        };
        var monthJson = JsonSerializer.Serialize(turkishMonths[date.Month - 1]);
        var yearJson = JsonSerializer.Serialize(date.Year.ToString());

        var result = await EvaluateScriptAsync(
            $$"""
            (() => {
                const menu = Array.from(document.querySelectorAll('.dp__menu, .dp__outer_menu_wrap'))
                    .find(m => {
                        const s = window.getComputedStyle(m);
                        const r = m.getBoundingClientRect();
                        return s.display !== 'none' && s.visibility !== 'hidden' && r.width > 0;
                    });
                if (!menu) return false;

                const dayTarget = {{dayJson}};
                const monthTarget = {{monthJson}};
                const yearTarget = {{yearJson}};

                const allCalendars = Array.from(menu.querySelectorAll('.dp__calendar'));
                let searchRoot = null;

                for (const cal of allCalendars) {
                    const hdr = cal.querySelector('.dp__month_year_select, .dp__calendar_header_item, .dp__month_year_wrap, .dp__calendar_header');
                    const hdrText = (hdr?.textContent || '').trim();
                    if (hdrText.includes(monthTarget) && hdrText.includes(yearTarget)) {
                        searchRoot = cal;
                        break;
                    }
                }
                if (!searchRoot) searchRoot = menu;

                const selectors = [
                    '.dp__cell_inner',
                    '.dp__calendar_item button',
                    '.dp__calendar_item > div',
                    '.dp__calendar_item'
                ];

                for (const sel of selectors) {
                    const candidates = Array.from(searchRoot.querySelectorAll(sel))
                        .filter(c => {
                            const text = (c.textContent || '').trim();
                            const num = parseInt(text, 10);
                            if (!text || isNaN(num)) return false;
                            const item = c.closest('.dp__calendar_item') ?? c;
                            return !item.classList.contains('dp__cell_offset') &&
                                   !item.classList.contains('dp__cell_disabled') &&
                                   !c.classList.contains('dp__cell_offset') &&
                                   !c.classList.contains('dp__cell_disabled');
                        });

                    const cell = candidates.find(c => parseInt((c.textContent || '').trim(), 10) === dayTarget);
                    if (cell) {
                        cell.scrollIntoView({ block: 'nearest', inline: 'nearest' });
                        const rect = cell.getBoundingClientRect();
                        const x = rect.left + rect.width / 2;
                        const y = rect.top + rect.height / 2;
                        const opts = { bubbles: true, cancelable: true, view: window, clientX: x, clientY: y };

                        if (typeof PointerEvent === 'function') {
                            cell.dispatchEvent(new PointerEvent('pointerdown', { ...opts, pointerId: 1, pointerType: 'mouse', isPrimary: true, buttons: 1 }));
                            cell.dispatchEvent(new PointerEvent('pointerup', { ...opts, pointerId: 1, pointerType: 'mouse', isPrimary: true }));
                        }
                        cell.dispatchEvent(new MouseEvent('mouseover', opts));
                        cell.dispatchEvent(new MouseEvent('mousedown', { ...opts, buttons: 1 }));
                        cell.dispatchEvent(new MouseEvent('mouseup', opts));
                        cell.click();
                        return true;
                    }
                }
                return false;
            })();
            """);

        return IsScriptTrue(result);
    }

    private async Task ConfirmDatePickerAsync()
    {
        await EvaluateScriptAsync(
            """
            (() => {
                const selectBtn = document.querySelector('.dp__action_select, button.dp__action_select, .dp__select');
                if (selectBtn) {
                    selectBtn.click();
                    return true;
                }
                return false;
            })();
            """);
    }

    public async Task SelectTimeAsync(int timePickerIndex, string time)
    {
        if (string.IsNullOrWhiteSpace(time)) return;

        Report($"Saat seçimi yapılıyor (index {timePickerIndex}): {time}");
        var timeJson = JsonSerializer.Serialize(time.Trim());
        var indexJson = JsonSerializer.Serialize(timePickerIndex);

        var opened = await EvaluateScriptAsync(
            $$"""
            (() => {
                const groups = document.querySelectorAll('[modaltitle="Alış ve Bırakış Tarihi"], [modaltitlecmskey="pickup_and_dropoff_date"]');
                if (groups.length > {{indexJson}}) {
                    const group = groups[{{indexJson}}];
                    const timeBox = group.querySelectorAll(':scope > div')[1] || group.querySelector('select, input, div[class*="time"]');
                    if (timeBox) {
                        timeBox.click();
                        return true;
                    }
                }

                const timeElements = Array.from(document.querySelectorAll('div, select, button, input'))
                    .filter(el => {
                        const txt = (el.textContent || el.value || '').trim();
                        const style = window.getComputedStyle(el);
                        const rect = el.getBoundingClientRect();
                        return rect.width > 0 && rect.height > 0 && style.display !== 'none' && /^\d{2}:\d{2}$/.test(txt);
                    });

                if (timeElements.length > {{indexJson}}) {
                    const targetEl = timeElements[{{indexJson}}];
                    targetEl.scrollIntoView({ block: 'nearest', inline: 'nearest' });
                    targetEl.click();
                    return true;
                }

                return false;
            })();
            """);

        if (!IsScriptTrue(opened))
        {
            Report($"Saat kutusu [{timePickerIndex}] tetiklenemedi veya açılamadı.");
            return;
        }

        await Task.Delay(400);

        var selected = await EvaluateScriptAsync(
            $$"""
            (() => {
                const target = {{timeJson}};
                const visible = el => {
                    const r = el.getBoundingClientRect();
                    const s = window.getComputedStyle(el);
                    return r.width > 0 && r.height > 0 && s.display !== 'none' && s.visibility !== 'hidden';
                };

                const options = Array.from(document.querySelectorAll('.dropdown-item, [role="option"], li, .time-option, div[class*="option"], div[class*="item"]'))
                    .filter(visible);

                let found = options.find(o => {
                    const txt = (o.textContent || '').trim();
                    return txt === target || txt.startsWith(target);
                });

                if (!found) {
                    const allLeafs = Array.from(document.querySelectorAll('div, li, span, button'))
                        .filter(el => {
                            if (!visible(el)) return false;
                            const t = (el.textContent || '').trim();
                            return (t === target || t.startsWith(target)) && el.children.length === 0;
                        });
                    if (allLeafs.length > 0) found = allLeafs[0];
                }

                if (found) {
                    found.scrollIntoView({ block: 'nearest', inline: 'nearest' });
                    const rect = found.getBoundingClientRect();
                    const x = rect.left + rect.width / 2;
                    const y = rect.top + rect.height / 2;
                    const opts = { bubbles: true, cancelable: true, view: window, clientX: x, clientY: y };

                    if (typeof PointerEvent === 'function') {
                        found.dispatchEvent(new PointerEvent('pointerdown', { ...opts, pointerId: 1, pointerType: 'mouse', isPrimary: true, buttons: 1 }));
                        found.dispatchEvent(new PointerEvent('pointerup', { ...opts, pointerId: 1, pointerType: 'mouse', isPrimary: true }));
                    }
                    found.dispatchEvent(new MouseEvent('mousedown', { ...opts, buttons: 1 }));
                    found.dispatchEvent(new MouseEvent('mouseup', { ...opts }));
                    found.click();
                    return true;
                }

                return false;
            })();
            """);

        if (IsScriptTrue(selected))
        {
            Report($"Saat seçildi: {time}");
        }
        else
        {
            Report($"Saat '{time}' seçeneklerde bulunamadı.");
        }

        await Task.Delay(300);
    }

    public async Task ClickSearchButtonAsync()
    {
        Report("Araç Ara butonuna tıklanıyor...");

        await EvaluateScriptAsync(
            """
            (() => {
                if (document.activeElement && typeof document.activeElement.blur === 'function') {
                    document.activeElement.blur();
                }
                const menus = document.querySelectorAll('.dp__menu, .search-autocomplete');
                menus.forEach(m => {
                    if (m.style) m.style.display = 'none';
                });
            })();
            """);

        await Task.Delay(300);

        var result = await EvaluateScriptAsync(
            """
            (() => {
                const btn = document.querySelector('#search') ||
                            document.querySelector('button[type="submit"]') ||
                            Array.from(document.querySelectorAll('button')).find(b => (b.textContent || '').includes('Ara'));

                if (!btn) return JSON.stringify({ success: false, reason: 'Search button not found' });

                btn.scrollIntoView({ block: 'center', inline: 'center' });

                const rect = btn.getBoundingClientRect();
                const style = window.getComputedStyle(btn);
                const enabled = !btn.disabled &&
                    btn.getAttribute('aria-disabled') !== 'true' &&
                    style.pointerEvents !== 'none' &&
                    rect.width > 0 &&
                    rect.height > 0;

                if (!enabled) {
                    return JSON.stringify({ success: false, reason: 'Button disabled or hidden', text: (btn.textContent || '').trim() });
                }

                const x = rect.left + rect.width / 2;
                const y = rect.top + rect.height / 2;
                const opts = { bubbles: true, cancelable: true, view: window, clientX: x, clientY: y };

                if (typeof PointerEvent === 'function') {
                    btn.dispatchEvent(new PointerEvent('pointerdown', { ...opts, pointerId: 1, pointerType: 'mouse', isPrimary: true, buttons: 1 }));
                    btn.dispatchEvent(new PointerEvent('pointerup', { ...opts, pointerId: 1, pointerType: 'mouse', isPrimary: true }));
                }
                btn.dispatchEvent(new MouseEvent('mousedown', { ...opts, buttons: 1 }));
                btn.dispatchEvent(new MouseEvent('mouseup', { ...opts }));
                btn.dispatchEvent(new MouseEvent('click', opts));
                if (typeof btn.click === 'function') btn.click();

                return JSON.stringify({ success: true, text: (btn.textContent || '').trim() });
            })();
            """);

        Report($"Araç Ara buton tıklama sonucu: {result}");
        await Task.Delay(1000);
    }

    private static bool IsTargetMonthVisible(string headerText, DateTime target)
    {
        if (string.IsNullOrWhiteSpace(headerText))
            return false;

        var turkishMonths = new[]
        {
            "Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran",
            "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık"
        };

        var monthName = turkishMonths[target.Month - 1];
        var yearStr = target.Year.ToString();

        return headerText.Contains(monthName, StringComparison.OrdinalIgnoreCase)
            && headerText.Contains(yearStr);
    }

    private static bool ShouldGoBack(string headerText, DateTime target)
    {
        if (string.IsNullOrWhiteSpace(headerText))
            return false;

        foreach (var part in headerText.Split(' '))
        {
            if (int.TryParse(part, out var year))
            {
                if (year > target.Year) return true;
                if (year < target.Year) return false;
                break;
            }
        }

        var turkishMonths = new[]
        {
            "Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran",
            "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık"
        };

        for (var i = 0; i < turkishMonths.Length; i++)
        {
            if (headerText.Contains(turkishMonths[i], StringComparison.OrdinalIgnoreCase))
                return (i + 1) > target.Month;
        }

        return false;
    }

    private async Task<bool> IsPickupLocationSelectionAppliedAsync()
    {
        var pickupLocationInputSelectorJson = JsonSerializer.Serialize(PickupLocationInputSelector);
        var locationSuggestionSelectorJson = JsonSerializer.Serialize(LocationSuggestionSelector);
        var result = await EvaluateScriptAsync(
            $$"""
            (() => {
                const input = document.querySelector({{pickupLocationInputSelectorJson}});
                const visibleSuggestions = Array.from(document.querySelectorAll({{locationSuggestionSelectorJson}}))
                    .filter(item => {
                        const rect = item.getBoundingClientRect();
                        const style = getComputedStyle(item);
                        return rect.width > 0 &&
                            rect.height > 0 &&
                            style.display !== 'none' &&
                            style.visibility !== 'hidden';
                    });
                return !!input && input.value.trim().length > 0 && visibleSuggestions.length === 0;
            })();
            """);

        return IsScriptTrue(result);
    }

    private async Task WaitForLocationSuggestionsAsync(string selector, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        string? lastResult = null;
        var selectorJson = JsonSerializer.Serialize(selector);

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

    public async Task WaitForSearchResultsAsync(TimeSpan? timeout = null)
    {
        var deadline = DateTimeOffset.UtcNow + (timeout ?? TimeSpan.FromSeconds(30));
        Report("Arama sonuçlarının yüklenmesi bekleniyor...");

        while (DateTimeOffset.UtcNow < deadline)
        {
            var isReady = await EvaluateScriptAsync(
                """
                (() => {
                    const cards = document.querySelectorAll('#car_card_list .car-card, .car-card, .py-2.car-card');
                    const bodyText = (document.body.innerText || '').toLocaleLowerCase('tr-TR');
                    return cards.length > 0
                        || bodyText.includes('araç bulundu')
                        || bodyText.includes('hemen kirala')
                        || bodyText.includes('günlük fiyat');
                })();
                """);

            if (IsScriptTrue(isReady))
            {
                Report("Arama sonuçları sayfada göründü.");
                return;
            }

            await Task.Delay(500);
        }

        Report("Uyarı: Arama sonuç kartları zaman aşımı süresinde görünmedi.");
    }

    public async Task<List<SearchResultItem>> ReadSearchResultsAsync()
    {
        Report("Sonuç kartları okunuyor...");

        var jsonResult = await EvaluateScriptAsync(
            """
            (() => {
                const normalize = value => (value || '').replace(/\s+/g, ' ').trim();

                const cards = Array.from(document.querySelectorAll('#car_card_list .car-card, .car-card, .py-2.car-card'))
                    .filter(card => {
                        const rect = card.getBoundingClientRect();
                        const style = window.getComputedStyle(card);
                        return rect.width > 0 &&
                            rect.height > 0 &&
                            style.display !== 'none' &&
                            style.visibility !== 'hidden';
                    });

                const items = cards.map(card => {
                    const specs = Array.from(card.querySelectorAll('.icon-gear-type, .icon-gas-type'))
                        .map(icon => normalize(icon.parentElement?.textContent))
                        .filter(Boolean);

                    const title = normalize(card.querySelector('.text-dark-gray.text-lg.font-bold, .car-title, h3, h4')?.textContent);
                    const subtitle = normalize(card.querySelector('[data-cms-key="or_similar"], .car-subtitle')?.textContent);
                    const price = normalize(card.querySelector('#car_total_price, .price, .total-price')?.textContent);
                    const dailyPrice = normalize(card.querySelector('[data-cms-key="text_daily_price2"], .daily-price')?.textContent);
                    const transmission = specs.find(text => /manuel|otomatik/i.test(text)) || '';
                    const fuelType = specs.find(text => /benzin|dizel|hibrit|elektrik/i.test(text)) || '';
                    const supplier = normalize(card.querySelector('figure img[alt], .supplier-logo img')?.getAttribute('alt'));
                    const pickupInfo = normalize(card.querySelector('.icon-filled')?.parentElement?.textContent);
                    const actionText = normalize(card.querySelector('[data-cms-key="button_rent_now"], button')?.textContent);
                    const url = normalize(card.querySelector('a[href]')?.getAttribute('href'));

                    return {
                        title,
                        subtitle,
                        price,
                        dailyPrice,
                        transmission,
                        fuelType,
                        supplier,
                        pickupInfo,
                        actionText,
                        url
                    };
                }).filter(item => item.title || item.price);

                return JSON.stringify(items);
            })();
            """);

        if (string.IsNullOrWhiteSpace(jsonResult))
        {
            Report("Sayfada sonuç bulunamadı.");
            return new List<SearchResultItem>();
        }

        try
        {
            var cleanJson = jsonResult.Trim();
            if (cleanJson.StartsWith("\"") && cleanJson.EndsWith("\""))
            {
                cleanJson = JsonSerializer.Deserialize<string>(cleanJson) ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(cleanJson) || cleanJson == "[]")
            {
                Report("Sonuç listesi boş.");
                return new List<SearchResultItem>();
            }

            var items = JsonSerializer.Deserialize<List<SearchResultItem>>(cleanJson);
            Report($"{items?.Count ?? 0} sonuç başarıyla okundu.");
            return items ?? new List<SearchResultItem>();
        }
        catch (Exception ex)
        {
            Report($"Sonuç okuma JSON hatası: {ex.Message}");
            return new List<SearchResultItem>();
        }
    }

    public Task<string?> GetSearchDomDiagnosticAsync()
    {
        return EvaluateScriptAsync(
            """
            (() => {
                const compact = value => (value || '').replace(/\s+/g, ' ').trim();
                const inputs = Array.from(document.querySelectorAll('input, textarea'))
                    .slice(0, 20)
                    .map((el, index) => ({
                        index,
                        id: el.id || '',
                        name: el.getAttribute('name') || '',
                        type: el.getAttribute('type') || '',
                        placeholder: el.getAttribute('placeholder') || '',
                        value: el.value || '',
                        ariaLabel: el.getAttribute('aria-label') || '',
                        visible: (() => {
                            const rect = el.getBoundingClientRect();
                            return rect.width > 0 && rect.height > 0;
                        })()
                    }));

                const possibleLocationElements = Array.from(document.querySelectorAll('[id*="location" i], [placeholder*="alış" i], [placeholder*="teslim" i], [class*="location" i], [class*="autocomplete" i]'))
                    .slice(0, 20)
                    .map((el, index) => ({
                        index,
                        tag: el.tagName,
                        id: el.id || '',
                        className: el.className || '',
                        placeholder: el.getAttribute('placeholder') || '',
                        text: compact(el.textContent).slice(0, 120),
                        visible: (() => {
                            const rect = el.getBoundingClientRect();
                            return rect.width > 0 && rect.height > 0;
                        })()
                    }));

                return JSON.stringify({
                    url: location.href,
                    title: document.title,
                    inputCount: document.querySelectorAll('input, textarea').length,
                    pickupById: !!document.querySelector('#inputPickUpLocation'),
                    inputs,
                    possibleLocationElements
                });
            })();
            """);
    }

    private async Task WaitForScriptTrueAsync(string script, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;

        while (DateTimeOffset.UtcNow < deadline)
        {
            var result = await EvaluateScriptAsync(script);
            if (IsScriptTrue(result))
                return;

            await Task.Delay(250);
        }

        throw new TimeoutException($"Gömülü tarayıcı beklenen sayfa durumuna ulaşmadı. Son kontrol sonucu: {await EvaluateScriptAsync(script)}");
    }

    private void Report(string message)
    {
        Console.WriteLine($"[EmbeddedWebView] {message}");
        ProgressChanged?.Invoke(message);
    }

    private static string NormalizePhoneNumber(string raw)
    {
        var digits = new string(raw.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("90") && digits.Length == 12)
            digits = digits[2..];
        if (digits.StartsWith("0") && digits.Length == 11)
            digits = digits[1..];
        return digits;
    }

    private static List<string> SplitPhoneNumber(string number)
    {
        if (number.Length == 10)
        {
            return new List<string>
            {
                number.Substring(0, 3),
                number.Substring(3, 3),
                number.Substring(6, 2),
                number.Substring(8, 2)
            };
        }

        return new List<string> { number };
    }

    private static bool IsScriptTrue(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().Trim('"');
        return string.Equals(normalized, "true", StringComparison.OrdinalIgnoreCase);
    }
}

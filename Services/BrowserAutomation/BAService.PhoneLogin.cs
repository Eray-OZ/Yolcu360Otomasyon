namespace Yolcu360Otomasyon.Services;

public sealed partial class BAService
{
    private async Task InjectStealthAndHumanMouseScriptAsync()
    {
        await EvaluateScriptAsync(
            """
            (() => {
                if (window.__stealthInjected) return true;
                window.__stealthInjected = true;
    
                try { Object.defineProperty(navigator, 'webdriver', { get: () => undefined, configurable: true }); } catch {}
                try {
                    if (!window.chrome) {
                        window.chrome = { runtime: {}, loadTimes: function() {}, csi: function() {}, app: {} };
                    }
                } catch {}
                try {
                    Object.defineProperty(navigator, 'plugins', {
                        get: () => [
                            { name: 'Chrome PDF Viewer', filename: 'internal-pdf-viewer' },
                            { name: 'Chromium PDF Viewer', filename: 'internal-pdf-viewer' }
                        ],
                        configurable: true
                    });
                } catch {}
                try {
                    Object.defineProperty(navigator, 'languages', {
                        get: () => ['tr-TR', 'tr', 'en-US', 'en'],
                        configurable: true
                    });
                } catch {}

                window.__dispatchHumanMousePath = (targetX, targetY) => {
                    const el = document.elementFromPoint(targetX, targetY) || document.body;
                    const opts = { bubbles: true, cancelable: true, view: window, clientX: targetX, clientY: targetY, screenX: targetX + 50, screenY: targetY + 50 };
                    if (typeof PointerEvent === 'function') {
                        el.dispatchEvent(new PointerEvent('pointermove', { ...opts, pointerId: 1, pointerType: 'mouse', isPrimary: true }));
                    }
                    el.dispatchEvent(new MouseEvent('mousemove', opts));
                };

                window.__hasRecaptchaScoreError = () => {
                    const bodyText = (document.body.innerText || '').toLowerCase();
                    if (bodyText.includes('recaptcha_score_too_low') || bodyText.includes('recaptcha') || bodyText.includes('skor')) {
                        return true;
                    }
                    const toasts = Array.from(document.querySelectorAll('.toast, .notification, .alert, [role="alert"], div'));
                    return toasts.some(el => {
                        const txt = (el.textContent || '').toLowerCase();
                        return txt.includes('recaptcha') || txt.includes('score_too_low') || txt.includes('düşük');
                    });
                };

                return true;
            })();
            """);
    }

    public async Task LoginWithPhoneAsync(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            throw new InvalidOperationException("Telefon numarası boş bırakılamaz.");

        Report("Gömülü tarayıcıda Yolcu360 login sayfası açılıyor...");
        await NavigateAsync("https://www.yolcu360.com/login?redirect=%2F");
        await WaitForDocumentReadyAsync();
        await InjectStealthAndHumanMouseScriptAsync();
        await Task.Delay(Random.Shared.Next(1500, 2500));
        await CloseInitialPopupAsync();

        Report("Telefon numarası inputu bekleniyor...");
        await WaitForScriptTrueAsync(
            """
            (() => !!document.querySelector('#phn-input') || !!document.querySelector('input[type="tel"]'))();
            """,
            TimeSpan.FromSeconds(20));

        await InjectStealthAndHumanMouseScriptAsync();

        var normalizedPhone = NormalizePhoneNumber(phoneNumber);
        Report($"Telefon numarası insansı davranışla yazılıyor: {normalizedPhone}");

        await Task.Delay(350);

        await EvaluateScriptAsync(
            """
            (() => {
                const input = document.querySelector('#phn-input') || document.querySelector('input[type="tel"]');
                if (!input) return false;
                input.scrollIntoView({ block: 'center', inline: 'nearest' });
                input.focus();
                input.click();
                input.value = '';
                input.dispatchEvent(new Event('input', { bubbles: true }));
                return true;
            })();
            """);

        await Task.Delay(220);

        var phoneChunks = SplitPhoneNumber(normalizedPhone);
        foreach (var chunk in phoneChunks)
        {
            foreach (var ch in chunk)
            {
                var charJson = ToJson(ch.ToString());
                await EvaluateScriptAsync(
                    $$"""
                    (() => {
                        const input = document.querySelector('#phn-input') || document.querySelector('input[type="tel"]');
                        if (!input) return false;
                        const char = {{charJson}};
                        input.dispatchEvent(new KeyboardEvent('keydown', { bubbles: true, key: char }));
                        input.value = (input.value || '') + char;
                        input.dispatchEvent(new InputEvent('input', { bubbles: true, inputType: 'insertText', data: char }));
                        input.dispatchEvent(new Event('input', { bubbles: true }));
                        input.dispatchEvent(new KeyboardEvent('keyup', { bubbles: true, key: char }));
                        return true;
                    })();
                    """);
                await Task.Delay(Random.Shared.Next(110, 170));
            }
            await Task.Delay(Random.Shared.Next(180, 320));
        }

        await EvaluateScriptAsync(
            """
            (() => {
                const input = document.querySelector('#phn-input') || document.querySelector('input[type="tel"]');
                if (!input) return;
                input.dispatchEvent(new Event('change', { bubbles: true }));
                input.dispatchEvent(new Event('blur', { bubbles: true }));

                const btn = Array.from(document.querySelectorAll('button, input[type="submit"]'))
                    .find(b => (b.textContent || b.value || '').trim().toLowerCase().includes('devam'));
                if (btn) {
                    btn.disabled = false;
                    btn.removeAttribute('disabled');
                    btn.classList.remove('disabled');
                }
            })();
            """);

        Report("Telefon numarası girildi, reCAPTCHA v3 güven puanı oluşturuluyor...");
        for (var i = 0; i < 4; i++)
        {
            var rx = Random.Shared.Next(100, 500);
            var ry = Random.Shared.Next(100, 400);
            await EvaluateScriptAsync($"window.__dispatchHumanMousePath ? window.__dispatchHumanMousePath({rx}, {ry}) : null;");
            await Task.Delay(500);
        }

        Report("'Devam Et' butonuna insansı şekilde tıklanıyor...");
        var continueClicked = await EvaluateScriptAsync(
            """
            (() => {
                const btn = Array.from(document.querySelectorAll('button, input[type="submit"], [role="button"]'))
                    .find(b => {
                        const txt = (b.textContent || b.value || b.getAttribute('aria-label') || '').trim().toLowerCase();
                        return txt.includes('devam');
                    });
                if (!btn) return false;

                btn.scrollIntoView({ block: 'center', inline: 'nearest' });
                btn.disabled = false;
                btn.removeAttribute('disabled');
                btn.classList.remove('disabled');

                const rect = btn.getBoundingClientRect();
                const x = rect.left + rect.width / 2;
                const y = rect.top + rect.height / 2;
                const opts = { bubbles: true, cancelable: true, view: window, clientX: x, clientY: y, screenX: x + 50, screenY: y + 50 };

                if (typeof PointerEvent === 'function') {
                    btn.dispatchEvent(new PointerEvent('pointerdown', { ...opts, pointerId: 1, pointerType: 'mouse', isPrimary: true, buttons: 1 }));
                    btn.dispatchEvent(new PointerEvent('pointerup', { ...opts, pointerId: 1, pointerType: 'mouse', isPrimary: true }));
                }
                btn.dispatchEvent(new MouseEvent('mousedown', { ...opts, buttons: 1 }));
                btn.dispatchEvent(new MouseEvent('mouseup', opts));
                btn.click();

                const form = btn.closest('form');
                if (form) {
                    try { form.requestSubmit(); } catch { try { form.submit(); } catch {} }
                }
                return true;
            })();
            """);

        if (!IsScriptTrue(continueClicked))
            throw new InvalidOperationException("Gömülü tarayıcıda 'Devam Et' butonu tıklanamadı.");

        await Task.Delay(2500);

        var hasRecaptchaError = await EvaluateScriptAsync("window.__hasRecaptchaScoreError ? window.__hasRecaptchaScoreError() : false");
        if (IsScriptTrue(hasRecaptchaError))
        {
            Report("reCAPTCHA puan uyarısı algılandı (recaptcha_score_too_low). Insansı sayfa hareketleri artırılarak tekrar deneniyor...");
            for (var i = 0; i < 5; i++)
            {
                var rx = Random.Shared.Next(100, 500);
                var ry = Random.Shared.Next(100, 400);
                await EvaluateScriptAsync($"window.__dispatchHumanMousePath ? window.__dispatchHumanMousePath({rx}, {ry}) : null;");
                await Task.Delay(500);
            }

            Report("'Devam Et' butonuna 2. deneme tıklaması yapılıyor...");
            await EvaluateScriptAsync(
                """
                (() => {
                    const btn = Array.from(document.querySelectorAll('button, input[type="submit"]'))
                        .find(b => (b.textContent || b.value || '').trim().toLowerCase().includes('devam'));
                    if (!btn) return false;
                    btn.disabled = false;
                    btn.click();
                    return true;
                })();
                """);
            await Task.Delay(2000);
        }

        Report("SMS doğrulama ekranı bekleniyor...");
        await WaitForScriptTrueAsync(
            """
            (() => {
                const text = (document.body.innerText || '').toLocaleLowerCase('tr-TR');
                const hasSmsText = text.includes('doğrulama') || text.includes('sms') || text.includes('gönderilen') || text.includes('şifre') || text.includes('tek kullanımlık') || text.includes('verification code');

                const visible = el => {
                    const rect = el.getBoundingClientRect();
                    const style = window.getComputedStyle(el);
                    return rect.width > 0 && rect.height > 0 && style.display !== 'none' && style.visibility !== 'hidden';
                };

                const allInputs = Array.from(document.querySelectorAll('input, [contenteditable="true"]')).filter(visible);
                const otpLikeInputs = allInputs.filter(input => {
                    if (input.id === 'languageSearch' || input.id === 'inputPickUpLocation' || input.id === 'is_different_dropoff') return false;
                    const attrs = `${input.id} ${input.name} ${input.placeholder} ${input.autocomplete} ${input.inputMode} ${input.type} ${input.className}`.toLocaleLowerCase('tr-TR');
                    return attrs.includes('otp') || attrs.includes('code') || attrs.includes('kod') || attrs.includes('pin') || attrs.includes('verify') || attrs.includes('dogrulama') || attrs.includes('sms') || input.maxLength === 1;
                });

                return hasSmsText || otpLikeInputs.length > 0;
            })();
            """,
            TimeSpan.FromSeconds(30));
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
}

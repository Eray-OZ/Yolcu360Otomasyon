using System.Text.Json;
using Yolcu360Otomasyon.Models;

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
                    const errorKeywords = ['recaptcha_score_too_low', 'score_too_low', 'recaptcha puanı', 'güvenlik doğrulaması başarısız'];
                    const toasts = document.querySelectorAll('.toast, .notification, .alert, [role="alert"], .Toastify, [class*="toast"], [class*="snackbar"]');
                    for (const el of toasts) {
                        const txt = (el.textContent || '').toLowerCase();
                        if (errorKeywords.some(k => txt.includes(k))) return true;
                    }
                    // URL'de hata parametresi kontrolü
                    if (location.search.includes('recaptcha') || location.hash.includes('recaptcha')) return true;
                    return false;
                };

                return true;
            })();
            """);
    }

    public async Task LoginWithPhoneAsync(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            throw new InvalidOperationException("Telefon numarası boş bırakılamaz.");

        Report("Gömülü tarayıcıda Yolcu360 ana sayfası açılıyor (reCAPTCHA güven puanı ısındırılıyor)...");
        await NavigateAsync("https://www.yolcu360.com/");
        await WaitForDocumentReadyAsync();
        await EnsureJavaScriptHelpersAsync();
        await InjectStealthAndHumanMouseScriptAsync();
        await WaitForInitialPopupAndCloseAsync(TimeSpan.FromSeconds(3));

        // Warm up reCAPTCHA v3 score with human mouse movements on home page
        for (int i = 0; i < 3; i++)
        {
            var rx = Random.Shared.Next(100, 600);
            var ry = Random.Shared.Next(100, 400);
            await EvaluateScriptAsync($"window.__dispatchHumanMousePath ? window.__dispatchHumanMousePath({rx}, {ry}) : null;");
            await Task.Delay(400);
        }

        Report("Organik olarak 'Giriş Yap' butonuna tıklanıyor...");
        var headerLoginClicked = await EvaluateScriptAsync(
            """
            (() => {
                const links = Array.from(document.querySelectorAll('a, button, [role="button"]'));
                const loginBtn = links.find(el => {
                    const txt = (el.textContent || el.getAttribute('title') || '').trim().toLowerCase();
                    const href = (el.getAttribute('href') || '').toLowerCase();
                    return href.includes('/login') || txt === 'giriş yap' || txt.includes('giriş /') || txt.includes('üye ol');
                });
                if (!loginBtn) return false;
                if (window.__ba && window.__ba.clickLikeUser) {
                    window.__ba.clickLikeUser(loginBtn);
                } else {
                    loginBtn.click();
                }
                return true;
            })();
            """);

        if (!IsScriptTrue(headerLoginClicked))
        {
            Report("Header giriş butonu bulunamadı, login sayfasına doğrudan yönlendiriliyor...");
            await NavigateAsync("https://www.yolcu360.com/login?redirect=%2F");
            await WaitForDocumentReadyAsync();
        }
        else
        {
            await WaitForScriptTrueAsync(
                """
                (() => {
                    const phoneInput = document.querySelector('#phn-input, input[type="tel"]');
                    const isLoginUrl = location.href.includes('/login');

                    return document.readyState === 'complete' &&
                        (isLoginUrl || !!phoneInput);
                })();
                """,
                TimeSpan.FromSeconds(15));
        }

        await EnsureJavaScriptHelpersAsync();
        await InjectStealthAndHumanMouseScriptAsync();

        Report("Telefon numarası inputu bekleniyor...");
        await WaitForScriptTrueAsync(
            """
            (() => !!document.querySelector('#phn-input') || !!document.querySelector('input[type="tel"]'))();
            """,
            TimeSpan.FromSeconds(20));

        await InjectStealthAndHumanMouseScriptAsync();

        var normalizedPhone = NormalizePhoneNumber(phoneNumber);
        Report($"Telefon numarası insansı davranışla yazılıyor: {normalizedPhone}");

        await WaitForPhoneInputReadyAsync();

        await EvaluateScriptAsync(
            """
            (() => {
                const input = document.querySelector('#phn-input') || document.querySelector('input[type="tel"]');
                if (!input) return false;
                if (window.__ba?.microScroll) window.__ba.microScroll();
                try {
                    input.scrollIntoView({ behavior: 'smooth', block: 'center', inline: 'nearest' });
                } catch {
                    try { input.scrollIntoView({ block: 'center', inline: 'nearest' }); } catch {}
                }
                input.focus();
                input.click();
                input.value = '';
                input.dispatchEvent(new Event('input', { bubbles: true }));
                return true;
            })();
            """);

        await WaitForPhoneInputEmptyAsync();

        // Type phone number chunk by chunk (e.g. 538, 523, 28, 69) with human pauses
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
        for (int i = 0; i < 4; i++)
        {
            var rx = Random.Shared.Next(100, 500);
            var ry = Random.Shared.Next(100, 400);
            await EvaluateScriptAsync($"window.__dispatchHumanMousePath ? window.__dispatchHumanMousePath({rx}, {ry}) : null;");
            await Task.Delay(500);
        }

        Report("Google reCAPTCHA v3 servisinin hazır olması bekleniyor...");
        await WaitForScriptTrueOrTimeoutAsync(
            """
            (() => typeof window.grecaptcha !== 'undefined' && typeof window.grecaptcha.execute === 'function')();
            """,
            TimeSpan.FromSeconds(5));

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

                btn.disabled = false;
                btn.removeAttribute('disabled');
                btn.classList.remove('disabled');

                if (window.__ba && window.__ba.clickLikeUser) {
                    window.__ba.clickLikeUser(btn);
                } else {
                    btn.click();
                }

                const form = btn.closest('form');
                if (form) {
                    try { form.requestSubmit(); } catch { try { form.submit(); } catch {} }
                }

                return true;
            })();
            """);

        if (!IsScriptTrue(continueClicked))
            throw new InvalidOperationException("Gömülü tarayıcıda 'Devam Et' butonu tıklanamadı.");

        var hasRecaptchaError = await WaitForSmsScreenOrRecaptchaErrorAsync(TimeSpan.FromSeconds(8));
        if (hasRecaptchaError)
        {
            Report("reCAPTCHA puan uyarısı algılandı (recaptcha_score_too_low). Insansı sayfa hareketleri artırılarak tekrar deneniyor...");
            for (int i = 0; i < 5; i++)
            {
                var rx = Random.Shared.Next(100, 500);
                var ry = Random.Shared.Next(100, 400);
                await EvaluateScriptAsync($"window.__dispatchHumanMousePath ? window.__dispatchHumanMousePath({rx}, {ry}) : null;");
                await Task.Delay(500);
            }

            await WaitForScriptTrueOrTimeoutAsync(
                """
                (() => typeof window.grecaptcha !== 'undefined' && typeof window.grecaptcha.execute === 'function')();
                """,
                TimeSpan.FromSeconds(3));

            Report("'Devam Et' butonuna 2. deneme tıklaması yapılıyor...");
            await EvaluateScriptAsync(
                """
                (() => {
                    const btn = Array.from(document.querySelectorAll('button, input[type="submit"], [role="button"]'))
                        .find(b => {
                            const txt = (b.textContent || b.value || b.getAttribute('aria-label') || '').trim().toLowerCase();
                            return txt.includes('devam');
                        });
                    if (!btn) return false;

                    btn.disabled = false;
                    btn.removeAttribute('disabled');
                    btn.classList.remove('disabled');

                    if (window.__ba && window.__ba.clickLikeUser) {
                        window.__ba.clickLikeUser(btn);
                    } else {
                        btn.click();
                    }

                    const form = btn.closest('form');
                    if (form) {
                        try { form.requestSubmit(); } catch { try { form.submit(); } catch {} }
                    }

                    return true;
                })();
                """);
            await WaitForSmsScreenOrRecaptchaErrorAsync(TimeSpan.FromSeconds(8));
        }

        Report("SMS doğrulama ekranı bekleniyor...");
        await WaitForScriptTrueAsync(
            """
            (() => {
                const smsInput = document.querySelector('#sms_input');
                if (smsInput && window.__ba?.isVisible(smsInput)) return true;

                const text = (document.body.innerText || '').toLocaleLowerCase('tr-TR');
                const hasSmsText = text.includes('doğrulama') || text.includes('sms') || text.includes('gönderilen') || text.includes('şifre') || text.includes('tek kullanımlık');
                return hasSmsText;
            })();
            """,
            TimeSpan.FromSeconds(30));
    }

    private Task WaitForPhoneInputReadyAsync()
    {
        return WaitForScriptTrueAsync(
            """
            (() => {
                const input = document.querySelector('#phn-input, input[type="tel"]');
                return !!window.__ba?.isVisible(input) &&
                    !input.disabled &&
                    input.getAttribute('readonly') === null;
            })();
            """,
            TimeSpan.FromSeconds(10));
    }

    private Task WaitForPhoneInputEmptyAsync()
    {
        return WaitForScriptTrueAsync(
            """
            (() => {
                const input = document.querySelector('#phn-input, input[type="tel"]');
                return !!input && (input.value || '').trim().length === 0;
            })();
            """,
            TimeSpan.FromSeconds(5));
    }

    private async Task<bool> WaitForSmsScreenOrRecaptchaErrorAsync(TimeSpan timeout)
    {
        string lastState = "waiting";

        await WaitUntilAsync(
            async () =>
            {
                var state = await EvaluateScriptAsync(
                """
                (() => {
                    const smsInput = document.querySelector('#sms_input');
                    if (smsInput && window.__ba?.isVisible(smsInput)) return 'sms';

                    const text = (document.body.innerText || '').toLocaleLowerCase('tr-TR');
                    const hasSmsText = text.includes('doğrulama') || text.includes('sms') || text.includes('gönderilen') || text.includes('tek kullanımlık');
                    if (hasSmsText && !document.querySelector('#phn-input')) return 'sms';

                    if (window.__hasRecaptchaScoreError && window.__hasRecaptchaScoreError()) {
                        return 'recaptcha';
                    }

                    return 'waiting';
                })();
                """);

                lastState = (state ?? string.Empty).Trim().Trim('"');
                return !string.Equals(lastState, "waiting", StringComparison.OrdinalIgnoreCase);
            },
            timeout);

        if (string.Equals(lastState, "recaptcha", StringComparison.OrdinalIgnoreCase))
            return true;
        if (string.Equals(lastState, "sms", StringComparison.OrdinalIgnoreCase))
            return false;

        var hasRecaptchaError = await EvaluateScriptAsync("window.__hasRecaptchaScoreError ? window.__hasRecaptchaScoreError() : false");
        return IsScriptTrue(hasRecaptchaError);
    }

    private Task WaitForSmsCodeInputReadyAsync()
    {
        return WaitForScriptTrueAsync(
            """
            (() => {
                const input = document.querySelector('#sms_input');
                return !!window.__ba?.isVisible(input) &&
                    !input.disabled &&
                    input.getAttribute('readonly') === null;
            })();
            """,
            TimeSpan.FromSeconds(10));
    }

    private Task WaitForSmsVerificationButtonReadyAsync(TimeSpan timeout)
    {
        return WaitForScriptTrueAsync(
            """
            (() => {
                const button = document.querySelector('button[data-cms-key="button_apply"]');
                return !!window.__ba?.isVisible(button) &&
                    !button.disabled &&
                    button.getAttribute('aria-disabled') !== 'true';
            })();
            """,
            timeout);
    }

    public async Task FillSmsVerificationCodeAsync(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new InvalidOperationException("SMS doğrulama kodu boş olamaz.");

        Report($"Gömülü tarayıcıda SMS kodu yazılıyor: {code.Trim()}");
        await WaitForSmsCodeInputReadyAsync();

        var cleanCode = code.Trim();
        var codeJson = JsonSerializer.Serialize(cleanCode);

        var fillResultJson = await EvaluateScriptAsync(
            $$"""
            (() => {
                const code = {{codeJson}};
                const input = document.querySelector('#sms_input');

                if (!input) {
                    return JSON.stringify({
                        success: false,
                        reason: 'SMS input bulunamadı'
                    });
                }

                const isReady = !!window.__ba?.isVisible(input) &&
                    !input.disabled &&
                    input.getAttribute('readonly') === null;

                if (!isReady) {
                    return JSON.stringify({
                        success: false,
                        reason: 'SMS input hazır değil'
                    });
                }

                input.focus();
                input.click();

                const descriptor = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value');
                if (descriptor?.set) {
                    descriptor.set.call(input, code);
                } else {
                    input.value = code;
                }

                input.dispatchEvent(new InputEvent('input', {
                    bubbles: true,
                    inputType: 'insertText',
                    data: code
                }));

                input.dispatchEvent(new Event('change', { bubbles: true }));

                return JSON.stringify({
                    success: true,
                    type: 'sms_input',
                    id: input.id
                });
            })();
            """);

        Report($"SMS kutu dolum sonucu: {fillResultJson}");

        Report("SMS kodu yazıldı, doğrulama butonunun hazır olması bekleniyor...");
        await WaitForSmsVerificationButtonReadyAsync(TimeSpan.FromSeconds(8));

        Report("SMS doğrulama butonu tıklanıyor...");
        var clickResult = await EvaluateScriptAsync(
            """
            (() => {
                const button = document.querySelector('button[data-cms-key="button_apply"]');
                if (!window.__ba?.isVisible(button)) return false;

                button.disabled = false;
                button.removeAttribute('disabled');
                button.classList.remove('disabled');

                button.click();

                return true;
            })();
            """);

        Report(IsScriptTrue(clickResult) ? "SMS doğrulama butonu tıklandı." : "SMS doğrulama butonu bulunamadı, gömülü tarayıcıdan manuel tıklayabilirsiniz.");
    }

    public async Task WaitForLoginCompletedAsync(TimeSpan? timeout = null)
    {
        Report("Giriş işleminin tamamlanması bekleniyor...");

        var completed = await WaitForScriptTrueOrTimeoutAsync(
            """
            (() => {
                try {
                    const user = JSON.parse(localStorage.getItem('user') || 'null');
                    const token = JSON.parse(localStorage.getItem('token') || 'null');

                    return !!user &&
                        user.anonymous === false &&
                        !!token &&
                        typeof token.accessToken === 'string' &&
                        token.accessToken.length > 0;
                } catch {
                    return false;
                }
            })();
            """,
            timeout ?? TimeSpan.FromSeconds(30),
            TimeSpan.FromMilliseconds(500));

        Report(completed
            ? "Giriş başarıyla tamamlandı."
            : "Giriş tamamlanma kontrolü zaman aşımına uğradı, ancak devam ediliyor.");
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

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
                try { Object.defineProperty(navigator, 'platform', { get: () => 'MacIntel', configurable: true }); } catch {}
                try { Object.defineProperty(navigator, 'vendor', { get: () => 'Google Inc.', configurable: true }); } catch {}
                try { Object.defineProperty(navigator, 'maxTouchPoints', { get: () => 0, configurable: true }); } catch {}
                try { Object.defineProperty(navigator, 'hardwareConcurrency', { get: () => 8, configurable: true }); } catch {}
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

                // Canvas & WebGL fingerprint masking
                try {
                    const origGetImageData = CanvasRenderingContext2D.prototype.getImageData;
                    CanvasRenderingContext2D.prototype.getImageData = function(x, y, w, h) {
                        const imgData = origGetImageData.apply(this, arguments);
                        if (imgData && imgData.data && imgData.data.length > 4) {
                            imgData.data[imgData.data.length - 1] = (imgData.data[imgData.data.length - 1] ^ 1);
                        }
                        return imgData;
                    };
                } catch {}

                try {
                    const getParam = WebGLRenderingContext.prototype.getParameter;
                    WebGLRenderingContext.prototype.getParameter = function(param) {
                        if (param === 37445) return 'Apple Inc.';
                        if (param === 37446) return 'Apple GPU';
                        return getParam.apply(this, [param]);
                    };
                } catch {}

                window.__dispatchHumanMousePath = (targetX, targetY) => {
                    const el = document.elementFromPoint(targetX, targetY) || document.body;
                    const opts = { bubbles: true, cancelable: true, view: window, clientX: targetX, clientY: targetY, screenX: targetX + 50, screenY: targetY + 50 };
                    if (typeof PointerEvent === 'function') {
                        el.dispatchEvent(new PointerEvent('pointermove', { ...opts, pointerId: 1, pointerType: 'mouse', isPrimary: true }));
                    }
                    el.dispatchEvent(new MouseEvent('mousemove', opts));
                };

                window.__lastApiRecaptchaError = false;
                window.__clearRecaptchaError = () => { window.__lastApiRecaptchaError = false; };

                // Hook fetch to detect API error payload
                if (!window.__origFetch) {
                    window.__origFetch = window.fetch;
                    window.fetch = async function(...args) {
                        const response = await window.__origFetch.apply(this, args);
                        try {
                            const clone = response.clone();
                            clone.text().then(body => {
                                if (body && (body.includes('recaptcha_score_too_low') || body.includes('score_too_low'))) {
                                    window.__lastApiRecaptchaError = true;
                                }
                            }).catch(() => {});
                        } catch {}
                        return response;
                    };
                }

                // Hook XMLHttpRequest to detect API error payload
                if (!window.__origXhrOpen) {
                    window.__origXhrOpen = XMLHttpRequest.prototype.open;
                    window.__origXhrSend = XMLHttpRequest.prototype.send;
                    XMLHttpRequest.prototype.send = function(...args) {
                        this.addEventListener('load', function() {
                            try {
                                const body = this.responseText;
                                if (body && (body.includes('recaptcha_score_too_low') || body.includes('score_too_low'))) {
                                    window.__lastApiRecaptchaError = true;
                                }
                            } catch {}
                        });
                        return window.__origXhrSend.apply(this, args);
                    };
                }

                window.__hasRecaptchaScoreError = () => {
                    if (window.__lastApiRecaptchaError) return true;
                    const errorKeywords = ['recaptcha_score_too_low', 'score_too_low', 'recaptcha puanı', 'güvenlik doğrulaması başarısız', 'güvenlik kontrolü'];
                    const toasts = document.querySelectorAll('.toast, .notification, .alert, [role="alert"], .Toastify, [class*="toast"], [class*="snackbar"], [class*="modal"], [class*="error"]');
                    for (const el of toasts) {
                        const txt = (el.textContent || '').toLowerCase();
                        if (errorKeywords.some(k => txt.includes(k))) return true;
                    }
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

        // Warm up reCAPTCHA v3 score with human movements on home page
        await SimulateHumanWarmupAsync(steps: 3, baseDelayMs: 350);

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
        var typedPhone = string.Empty;
        foreach (var chunk in phoneChunks)
        {
            foreach (var ch in chunk)
            {
                var charJson = JsonSerializer.Serialize(ch.ToString());
                typedPhone += ch;
                var typedPhoneJson = JsonSerializer.Serialize(typedPhone);
                await EvaluateScriptAsync(
                    $$"""
                    (() => {
                        const input = document.querySelector('#phn-input') || document.querySelector('input[type="tel"]');
                        if (!input) return false;
                        const char = {{charJson}};
                        const value = {{typedPhoneJson}};
                        const descriptor = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value');
                        const setValue = nextValue => {
                            if (descriptor?.set) {
                                descriptor.set.call(input, nextValue);
                            } else {
                                input.value = nextValue;
                            }
                        };

                        input.dispatchEvent(new KeyboardEvent('keydown', { bubbles: true, key: char }));
                        setValue(value);
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

        var phoneApplied = await WaitForPhoneInputValueAsync(normalizedPhone, TimeSpan.FromSeconds(3));
        if (!phoneApplied)
            throw new InvalidOperationException("Telefon numarası inputa doğru yazılamadı.");

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

        const int maxSubmitAttempts = 3;
        bool smsScreenReached = false;

        for (int attempt = 1; attempt <= maxSubmitAttempts; attempt++)
        {
            if (attempt == 1)
            {
                Report("Telefon numarası girildi, reCAPTCHA v3 güven puanı oluşturuluyor...");
                await SimulateHumanWarmupAsync(steps: 4, baseDelayMs: 400);
            }
            else
            {
                // Exponential backoff: attempt 2 -> ~6-8s, attempt 3 -> ~12-15s
                var backoffBaseMs = (int)Math.Pow(2, attempt) * 1500;
                var jitterMs = Random.Shared.Next(500, 2000);
                var totalBackoffMs = backoffBaseMs + jitterMs;
                var warmupSteps = 6 + (attempt * 3);
                var stepDelay = totalBackoffMs / warmupSteps;

                Report($"reCAPTCHA puan uyarısı algılandı (recaptcha_score_too_low). {attempt}. deneme için {totalBackoffMs / 1000.0:F1}s insansı ısınma ve soğuma (backoff) uygulanıyor...");
                await SimulateHumanWarmupAsync(steps: warmupSteps, baseDelayMs: stepDelay);
            }

            Report("Google reCAPTCHA v3 servisinin hazır olması bekleniyor...");
            await WaitForScriptTrueOrTimeoutAsync(
                """
                (() => typeof window.grecaptcha !== 'undefined' && typeof window.grecaptcha.execute === 'function')();
                """,
                TimeSpan.FromSeconds(5));

            await PrewarmAndInjectRecaptchaTokenAsync();

            Report(attempt == 1 
                ? "'Devam Et' butonuna insansı şekilde tıklanıyor..." 
                : $"'Devam Et' butonuna {attempt}. deneme tıklaması yapılıyor...");

            // Önceki denemeden kalan hata bayraklarını temizle
            await EvaluateScriptAsync("window.__clearRecaptchaError ? window.__clearRecaptchaError() : null;");

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
            if (!hasRecaptchaError)
            {
                smsScreenReached = true;
                break;
            }
        }

        if (!smsScreenReached)
        {
            throw new InvalidOperationException("reCAPTCHA güvenlik doğrulaması aşılamadı (tüm denemeler tükendi). Lütfen birkaç dakika sonra tekrar deneyin.");
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

    private Task<bool> WaitForPhoneInputValueAsync(string expectedPhone, TimeSpan timeout)
    {
        var expectedPhoneJson = JsonSerializer.Serialize(expectedPhone);

        return WaitForScriptTrueOrTimeoutAsync(
            $$"""
            (() => {
                const input = document.querySelector('#phn-input, input[type="tel"]');
                if (!input) return false;

                const expected = {{expectedPhoneJson}};
                const actualDigits = (input.value || '').replace(/\D/g, '');
                return actualDigits.endsWith(expected);
            })();
            """,
            timeout,
            TimeSpan.FromMilliseconds(250));
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

        // Zaman aşımı durumunda son durumu kontrol et
        var screenCheck = await EvaluateScriptAsync(
            """
            (() => {
                const smsInput = document.querySelector('#sms_input');
                if (smsInput && window.__ba?.isVisible(smsInput)) return 'sms';

                const text = (document.body.innerText || '').toLocaleLowerCase('tr-TR');
                const hasSmsText = text.includes('doğrulama') || text.includes('sms') || text.includes('gönderilen') || text.includes('tek kullanımlık');
                if (hasSmsText && !document.querySelector('#phn-input')) return 'sms';

                if (window.__hasRecaptchaScoreError && window.__hasRecaptchaScoreError()) return 'recaptcha';

                // SMS inputu yok ve telefon kutusu hala ekrandaysa hata/tekrar deneme gereklidir
                const phnInput = document.querySelector('#phn-input, input[type="tel"]');
                if (phnInput && window.__ba?.isVisible(phnInput)) return 'recaptcha';

                return 'unknown';
            })();
            """);

        var finalState = (screenCheck ?? string.Empty).Trim().Trim('"');
        if (string.Equals(finalState, "sms", StringComparison.OrdinalIgnoreCase))
            return false;

        // SMS gelmediyse kesinlikle retry tetikle
        return true;
    }

    private async Task PrewarmAndInjectRecaptchaTokenAsync()
    {
        const string siteKey = "6LdSrcgqAAAAAMP-0Z98kPozm5Mqdo-kjhGnsSHI";
        const string action = "request_phone_auth_code";

        Report("reCAPTCHA v3 token ön-üretimi başlatılıyor...");

        // Adım 1: Orijinal execute'u kaydet ve token üret
        await EvaluateScriptAsync(
            $$"""
            (() => {
                if (typeof window.grecaptcha === 'undefined' || typeof window.grecaptcha.execute !== 'function') return;
                if (!window.__recaptchaOriginalExecute) {
                    window.__recaptchaOriginalExecute = window.grecaptcha.execute.bind(window.grecaptcha);
                }
                window.__prewarmedRecaptchaToken = null;
                window.__recaptchaOriginalExecute(
                    '{{siteKey}}',
                    { action: '{{action}}' }
                ).then(token => {
                    window.__prewarmedRecaptchaToken = token || 'empty';
                }).catch(() => {
                    window.__prewarmedRecaptchaToken = 'error';
                });
            })();
            """);

        // Adım 2: Token'ın üretilmesini bekle
        var tokenReady = await WaitForScriptTrueOrTimeoutAsync(
            """
            (() => !!window.__prewarmedRecaptchaToken)();
            """,
            TimeSpan.FromSeconds(5));

        if (!tokenReady)
        {
            Report("reCAPTCHA token ön-üretimi zaman aşımına uğradı, site kendi token'ını üretecek.");
            return;
        }

        // Adım 3: grecaptcha.execute'u one-shot override et
        var overrideResult = await EvaluateScriptAsync(
            """
            (() => {
                const token = window.__prewarmedRecaptchaToken;
                if (!token || token === 'error' || token === 'empty') return 'skip';

                window.grecaptcha.execute = function(siteKey, options) {
                    const prewarmed = window.__prewarmedRecaptchaToken;
                    if (prewarmed && prewarmed !== 'error' && prewarmed !== 'empty') {
                        window.__prewarmedRecaptchaToken = null;
                        window.grecaptcha.execute = window.__recaptchaOriginalExecute;
                        return Promise.resolve(prewarmed);
                    }
                    return window.__recaptchaOriginalExecute(siteKey, options);
                };

                return 'injected';
            })();
            """);

        var result = (overrideResult ?? string.Empty).Trim().Trim('"');
        Report(result == "injected"
            ? "reCAPTCHA v3 ön-üretilmiş token enjekte edildi. Site bu token'ı kullanacak."
            : $"reCAPTCHA token enjeksiyonu atlandı ({result}), site kendi token'ını üretecek.");
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

    private async Task SimulateHumanWarmupAsync(int steps, int baseDelayMs)
    {
        for (int i = 0; i < steps; i++)
        {
            var rx = Random.Shared.Next(120, 750);
            var ry = Random.Shared.Next(150, 550);
            
            await EvaluateScriptAsync(
                $$"""
                (() => {
                    if (window.__dispatchHumanMousePath) {
                        window.__dispatchHumanMousePath({{rx}}, {{ry}});
                    }
                    if ({{(i % 2 == 0 ? "true" : "false")}}) {
                        const deltaY = (Math.random() * 40) - 20;
                        try { window.scrollBy({ top: deltaY, behavior: 'smooth' }); } catch { try { window.scrollBy(0, deltaY); } catch {} }
                    }
                })();
                """);

            var delay = Random.Shared.Next(Math.Max(150, baseDelayMs - 100), baseDelayMs + 200);
            await Task.Delay(delay);
        }
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

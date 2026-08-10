using System.Text.Json;
using Yolcu360Otomasyon.Models;

namespace Yolcu360Otomasyon.Services;

public sealed partial class EmbeddedBrowserAutomationService
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

        // Focus and clear input
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

        // Type phone number chunk by chunk (e.g. 538, 523, 28, 69) with human pauses
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

        // Trigger change & blur and ensure button is enabled
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

        // Warmup delay for reCAPTCHA v3 telemetry
        Report("Telefon numarası girildi, reCAPTCHA v3 güven puanı oluşturuluyor...");
        for (int i = 0; i < 4; i++)
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

        // Check if recaptcha error occurred
        var hasRecaptchaError = await EvaluateScriptAsync("window.__hasRecaptchaScoreError ? window.__hasRecaptchaScoreError() : false");
        if (IsScriptTrue(hasRecaptchaError))
        {
            Report("reCAPTCHA puan uyarısı algılandı (recaptcha_score_too_low). Insansı sayfa hareketleri artırılarak tekrar deneniyor...");
            for (int i = 0; i < 5; i++)
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

    public async Task FillSmsVerificationCodeAsync(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new InvalidOperationException("SMS doğrulama kodu boş olamaz.");

        Report($"Gömülü tarayıcıda SMS kodu yazılıyor: {code.Trim()}");
        await Task.Delay(Random.Shared.Next(800, 1400));

        var cleanCode = code.Trim();
        var codeJson = ToJson(cleanCode);

        var fillResultJson = await EvaluateScriptAsync(
            $$"""
            (() => {
                const code = {{codeJson}};
                const normalize = value => (value || '').toLocaleLowerCase('tr-TR');
                const visible = el => {
                    if (!el) return false;
                    const rect = el.getBoundingClientRect();
                    const style = window.getComputedStyle(el);
                    return rect.width > 0 && rect.height > 0 && style.display !== 'none' && style.visibility !== 'hidden' && style.opacity !== '0';
                };

                const isExcluded = el => {
                    const id = (el.id || '').toLowerCase();
                    const name = (el.name || '').toLowerCase();
                    const placeholder = (el.placeholder || '').toLowerCase();
                    const className = (el.className || '').toLowerCase();
                    const type = (el.type || '').toLowerCase();
                    if (type === 'checkbox' || type === 'radio' || type === 'hidden' || type === 'submit' || type === 'button') return true;
                    return id.includes('languagesearch') || id.includes('pickuplocation') || id.includes('dropoff') ||
                           id.includes('search') || name.includes('search') || placeholder.includes('ara') ||
                           placeholder.includes('teslim') || placeholder.includes('alış') || className.includes('search');
                };

                const setValue = (el, value) => {
                    const prototype = el instanceof HTMLInputElement || el instanceof HTMLTextAreaElement ? Object.getPrototypeOf(el) : null;
                    const descriptor = prototype ? Object.getOwnPropertyDescriptor(prototype, 'value') : null;
                    if (descriptor?.set) {
                        descriptor.set.call(el, value);
                    } else if ('value' in el) {
                        el.value = value;
                    } else {
                        el.textContent = value;
                    }
                };

                const dispatchInput = (el, char) => {
                    el.focus();
                    el.dispatchEvent(new KeyboardEvent('keydown', { bubbles: true, key: char }));
                    el.dispatchEvent(new Event('input', { bubbles: true }));
                    el.dispatchEvent(new Event('change', { bubbles: true }));
                    el.dispatchEvent(new KeyboardEvent('keyup', { bubbles: true, key: char }));
                    el.dispatchEvent(new KeyboardEvent('keypress', { bubbles: true, key: char }));
                    el.dispatchEvent(new Event('blur', { bubbles: true }));
                };

                const allInputs = Array.from(document.querySelectorAll('input, [contenteditable="true"]')).filter(el => visible(el) && !isExcluded(el));

                const otpLikeInputs = allInputs.filter(input => {
                    const attrs = normalize(`${input.id} ${input.name} ${input.placeholder} ${input.autocomplete} ${input.inputMode} ${input.type} ${input.className} ${input.getAttribute?.('aria-label') || ''}`);
                    return attrs.includes('otp')
                        || attrs.includes('code')
                        || attrs.includes('kod')
                        || attrs.includes('pin')
                        || attrs.includes('verify')
                        || attrs.includes('dogrulama')
                        || attrs.includes('sms')
                        || input.maxLength === 1;
                });

                // 1. Single char digit inputs (e.g. 4 or 6 boxes)
                const singleCharInputs = (otpLikeInputs.length > 0 ? otpLikeInputs : allInputs).filter(input => input.maxLength === 1);
                if (singleCharInputs.length >= code.length) {
                    singleCharInputs.slice(0, code.length).forEach((input, index) => {
                        input.focus();
                        input.click?.();
                        setValue(input, code[index]);
                        dispatchInput(input, code[index]);
                    });
                    return JSON.stringify({ success: true, type: "single_char_boxes", count: singleCharInputs.length });
                }

                // 2. Single text input field
                const singleInput = otpLikeInputs.find(input => input.maxLength !== 1)
                    || allInputs.find(input => {
                        const attrs = normalize(`${input.id} ${input.name} ${input.placeholder} ${input.autocomplete} ${input.inputMode} ${input.type} ${input.className}`);
                        return attrs.includes('otp') || attrs.includes('code') || attrs.includes('kod') || attrs.includes('pin') || attrs.includes('verify') || attrs.includes('dogrulama') || attrs.includes('sms');
                    })
                    || allInputs.find(input => {
                        const type = normalize(input.type);
                        return type === 'tel' || type === 'text' || type === 'number';
                    });

                if (singleInput) {
                    singleInput.focus();
                    singleInput.click?.();
                    setValue(singleInput, code);
                    dispatchInput(singleInput, code[code.length - 1]);
                    return JSON.stringify({ success: true, type: "single_input", id: singleInput.id || singleInput.className || 'input' });
                }

                return JSON.stringify({ success: false, reason: "SMS kutusu bulunamadı", totalInputs: allInputs.length });
            })();
            """);

        Report($"SMS kutu dolum sonucu: {fillResultJson}");

        Report("SMS kodu yazıldı, doğrulama butonuna basmadan önce 3.5 saniye bekleniyor...");
        await Task.Delay(Random.Shared.Next(3200, 4200));

        Report("SMS doğrulama butonu tıklanıyor...");
        var clickResult = await EvaluateScriptAsync(
            """
            (() => {
                const applyBtn = document.querySelector('button[data-cms-key="button_apply"]');
                if (applyBtn) {
                    applyBtn.disabled = false;
                    applyBtn.click();
                    return true;
                }

                const visible = el => {
                    const rect = el.getBoundingClientRect();
                    const style = window.getComputedStyle(el);
                    return rect.width > 0 && rect.height > 0 && style.display !== 'none' && style.visibility !== 'hidden';
                };

                const buttons = Array.from(document.querySelectorAll('button, input[type="submit"]')).filter(b => {
                    if (!visible(b)) return false;
                    const txt = (b.textContent || b.value || '').trim().toLowerCase();
                    return txt.includes('doğrula') || txt.includes('onayla') || txt.includes('devam') || txt.includes('giriş') || txt.includes('gönder');
                });

                if (buttons.length > 0) {
                    const btn = buttons[0];
                    btn.disabled = false;
                    btn.click();
                    return true;
                }
                return false;
            })();
            """);

        Report(IsScriptTrue(clickResult) ? "SMS doğrulama butonu tıklandı." : "SMS doğrulama butonu bulunamadı, gömülü tarayıcıdan manuel tıklayabilirsiniz.");
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

    public async Task ClearBrowserSessionAsync()
    {
        Report("Gömülü tarayıcı oturumu ve çerezleri temizleniyor...");
        try
        {
            await NavigateAsync("https://www.yolcu360.com/logout");
            await Task.Delay(LogoutNavigationDelay);

            await EvaluateScriptAsync(
                """
                (() => {
                    try {
                        const domains = ['', '.yolcu360.com', 'www.yolcu360.com', 'yolcu360.com'];
                        const paths = ['/', '/login', '/api'];
                        const cookies = document.cookie.split(";");
                        for (let i = 0; i < cookies.length; i++) {
                            const cookie = cookies[i];
                            const eqPos = cookie.indexOf("=");
                            const name = eqPos > -1 ? cookie.substr(0, eqPos).trim() : cookie.trim();
                            for (const d of domains) {
                                for (const p of paths) {
                                    document.cookie = `${name}=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=${p}${d ? `; domain=${d}` : ''}`;
                                }
                            }
                        }
                    } catch {}
                    try { localStorage.clear(); } catch {}
                    try { sessionStorage.clear(); } catch {}
                    try {
                        if (window.indexedDB && window.indexedDB.databases) {
                            window.indexedDB.databases().then(dbs => {
                                for (const db of dbs) {
                                    if (db.name) window.indexedDB.deleteDatabase(db.name);
                                }
                            });
                        }
                    } catch {}
                    return true;
                })();
                """);
            await Task.Delay(800);
            Report("Gömülü tarayıcı çerezleri ve yerel depolama başarıyla temizlendi.");
        }
        catch (Exception ex)
        {
            Report($"Oturum temizleme hatası: {ex.Message}");
        }
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

            var json = ToJson(state, new JsonSerializerOptions { WriteIndented = true });
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
                    var partJson = ToJson(part.Trim() + "; path=/; domain=.yolcu360.com");
                    await EvaluateScriptAsync($"document.cookie = {partJson};");
                }
            }

            if (state.LocalStorage.Count > 0)
            {
                var localJson = ToJson(state.LocalStorage);
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
                var sessionJson = ToJson(state.SessionStorage);
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

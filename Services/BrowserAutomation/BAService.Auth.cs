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
        await WaitForInitialPopupAndCloseAsync(TimeSpan.FromSeconds(5));

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

                btn.click();

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
                    btn.click();

                    return true;
                })();
                """);
            await WaitForSmsScreenOrRecaptchaErrorAsync(TimeSpan.FromSeconds(8));
        }

        Report("SMS doğrulama ekranı bekleniyor...");
        await WaitForScriptTrueAsync(
            """
            (() => {
                const input = document.querySelector('#sms_input');

                if (!input) return false;

                const rect = input.getBoundingClientRect();
                const style = getComputedStyle(input);

                return rect.width > 0 &&
                    rect.height > 0 &&
                    style.display !== 'none' &&
                    style.visibility !== 'hidden';
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
                if (!input) return false;
                const rect = input.getBoundingClientRect();
                const style = getComputedStyle(input);
                return rect.width > 0 &&
                    rect.height > 0 &&
                    style.display !== 'none' &&
                    style.visibility !== 'hidden' &&
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
                    if (window.__hasRecaptchaScoreError && window.__hasRecaptchaScoreError()) {
                        return 'recaptcha';
                    }

                    const smsInput = document.querySelector('#sms_input');
                    if (!smsInput) return 'waiting';

                    const rect = smsInput.getBoundingClientRect();
                    const style = getComputedStyle(smsInput);

                    const isVisible = rect.width > 0 &&
                        rect.height > 0 &&
                        style.display !== 'none' &&
                        style.visibility !== 'hidden';

                    return isVisible ? 'sms' : 'waiting';
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
                if (!input) return false;

                const rect = input.getBoundingClientRect();
                const style = getComputedStyle(input);

                return rect.width > 0 &&
                    rect.height > 0 &&
                    style.display !== 'none' &&
                    style.visibility !== 'hidden' &&
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
                if (!button) return false;

                const rect = button.getBoundingClientRect();
                const style = getComputedStyle(button);

                return rect.width > 0 &&
                    rect.height > 0 &&
                    style.display !== 'none' &&
                    style.visibility !== 'hidden' &&
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

                const rect = input.getBoundingClientRect();
                const style = getComputedStyle(input);

                const isReady = rect.width > 0 &&
                    rect.height > 0 &&
                    style.display !== 'none' &&
                    style.visibility !== 'hidden' &&
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
                if (!button) return false;

                const rect = button.getBoundingClientRect();
                const style = getComputedStyle(button);

                const isVisible = rect.width > 0 &&
                    rect.height > 0 &&
                    style.display !== 'none' &&
                    style.visibility !== 'hidden';

                if (!isVisible) return false;

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
                const url = window.location.href;
                const text = (document.body.innerText || '').toLocaleLowerCase('tr-TR');
                return !url.includes('login') || text.includes('hesabım') || text.includes('profil') || text.includes('hoş geldin');
            })();
            """,
            timeout ?? TimeSpan.FromSeconds(30),
            TimeSpan.FromMilliseconds(500));

        Report(completed
            ? "Giriş başarıyla tamamlandı."
            : "Giriş tamamlanma kontrolü zaman aşımına uğradı, ancak devam ediliyor.");
    }

    public async Task ClearBrowserSessionAsync()
    {
        Report("Gömülü tarayıcı oturumu ve çerezleri temizleniyor...");
        try
        {
            await NavigateAsync("https://www.yolcu360.com/logout");
            await WaitForDocumentReadyAsync(TimeSpan.FromSeconds(10));

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
            await WaitForScriptTrueOrTimeoutAsync(
                """
                (() => {
                    try {
                        return localStorage.length === 0 && sessionStorage.length === 0;
                    } catch {
                        return true;
                    }
                })();
                """,
                TimeSpan.FromSeconds(3));
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

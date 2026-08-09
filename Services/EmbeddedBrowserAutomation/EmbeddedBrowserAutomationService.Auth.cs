using System.Text.Json;
using Yolcu360Otomasyon.Models;

namespace Yolcu360Otomasyon.Services;

public sealed partial class EmbeddedBrowserAutomationService
{
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

        // Wait 2.8 seconds
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

        // Entire code fallback injection via React setter
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

    public async Task ClearBrowserSessionAsync()
    {
        Report("Gömülü tarayıcı oturumu ve çerezleri temizleniyor...");
        try
        {
            await NavigateAsync("https://www.yolcu360.com/logout");
            await Task.Delay(1200);

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

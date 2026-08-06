using System.Text.Json;
using PuppeteerSharp;

namespace Yolcu360Otomasyon.Services;

public sealed partial class BrowserAutomationService
{
    public async Task LoginWithPhoneAsync(string phoneNumber)
    {
        var page = GetPage();

        if (string.IsNullOrWhiteSpace(phoneNumber))
            throw new InvalidOperationException("Telefon numarası boş bırakılamaz.");

        await page.GoToAsync("https://www.yolcu360.com/login?redirect=%2F", WaitUntilNavigation.Networkidle2);
        await CloseInitialPopupAsync();

        await page.WaitForSelectorAsync(Selectors.LoginPagePhoneInput, new WaitForSelectorOptions
        {
            Visible = true,
            Timeout = 30_000
        });

        var normalizedPhone = NormalizePhoneNumber(phoneNumber);
        await TypePhoneNumberHumanLikeAsync(Selectors.LoginPagePhoneInput, normalizedPhone);

        await WaitAsync(2_800);

        var recaptchaResponseTask = page.WaitForResponseAsync(
            response => response.Url.Contains(LoginRecaptchaEndpoint, StringComparison.OrdinalIgnoreCase),
            new WaitForOptions { Timeout = 15_000 });

        var continueClicked = await ClickButtonByTextHumanLikeAsync("Devam Et");

        if (!continueClicked)
            throw new InvalidOperationException("Login sayfasında 'Devam Et' butonu bulunamadı.");

        try
        {
            var recaptchaResponse = await recaptchaResponseTask;
            await ReportRecaptchaResponseAsync(recaptchaResponse);
        }
        catch (WaitTaskTimeoutException)
        {
            Report("reCAPTCHA cevabı 15 saniye içinde alınmadı.");
        }

        await page.WaitForFunctionAsync(
        """
        () => document.body.innerText.toLocaleLowerCase('tr-TR').includes('doğrulama kodu')
        """, new WaitForFunctionOptions { Timeout = 20000 });
    }

    public async Task<bool> IsSmsVerificationRequiredAsync()
    {
        var page = GetPage();

        try
        {
            return await page.EvaluateExpressionAsync<bool>(
                """
                (() => {
                    const text = document.body.innerText.toLocaleLowerCase('tr-TR');
                    const otpInputs = Array.from(document.querySelectorAll('input'))
                        .filter(input => {
                            const rect = input.getBoundingClientRect();
                            const style = window.getComputedStyle(input);
                            if (rect.width <= 0 || rect.height <= 0) return false;
                            if (style.display === 'none' || style.visibility === 'hidden') return false;

                            const attrs = `${input.id} ${input.name} ${input.placeholder} ${input.autocomplete} ${input.inputMode} ${input.type}`.toLocaleLowerCase('tr-TR');
                            return attrs.includes('otp')
                                || attrs.includes('code')
                                || attrs.includes('kod')
                                || input.maxLength === 1;
                        });

                    return otpInputs.length > 0
                        || text.includes('doğrulama kodu')
                        || text.includes('sms doğrulama')
                        || text.includes('tek kullanımlık')
                        || text.includes('verification code');
                })();
                """);
        }
        catch
        {
            return false;
        }
    }

    public async Task FillSmsVerificationCodeAsync(string code)
    {
        var page = GetPage();

        if (string.IsNullOrWhiteSpace(code))
            throw new InvalidOperationException("SMS doğrulama kodu boş.");

        Report($"SMS kodu giriliyor: {code}");

        try
        {
            await page.WaitForFunctionAsync(
                """
                () => {
                    const visible = el => {
                        const rect = el.getBoundingClientRect();
                        const style = window.getComputedStyle(el);
                        return rect.width > 0 &&
                            rect.height > 0 &&
                            style.display !== 'none' &&
                            style.visibility !== 'hidden';
                    };

                    const text = (document.body.innerText || '').toLocaleLowerCase('tr-TR');
                    const inputs = Array.from(document.querySelectorAll('input, [contenteditable="true"]')).filter(visible);
                    return inputs.length > 0
                        || text.includes('doğrulama kodu')
                        || text.includes('sms doğrulama')
                        || text.includes('telefonunuza')
                        || text.includes('6 haneli');
                }
                """,
                new WaitForFunctionOptions { Timeout = 30_000 });
        }
        catch (WaitTaskTimeoutException)
        {
            throw new InvalidOperationException("SMS doğrulama ekranı zamanında açılmadı.");
        }

        await WaitAsync(1_000);

        var filled = await page.EvaluateFunctionAsync<bool>(
            """
            (code) => {
                const normalize = value => (value || '').toLocaleLowerCase('tr-TR');
                const visible = el => {
                    const rect = el.getBoundingClientRect();
                    const style = window.getComputedStyle(el);
                    return rect.width > 0 &&
                        rect.height > 0 &&
                        style.display !== 'none' &&
                        style.visibility !== 'hidden';
                };

                const setValue = (el, value) => {
                    const prototype = el instanceof HTMLInputElement || el instanceof HTMLTextAreaElement
                        ? Object.getPrototypeOf(el)
                        : null;
                    const descriptor = prototype ? Object.getOwnPropertyDescriptor(prototype, 'value') : null;
                    if (descriptor?.set) {
                        descriptor.set.call(el, value);
                    } else if ('value' in el) {
                        el.value = value;
                    } else {
                        el.textContent = value;
                    }
                };

                const dispatchInput = el => {
                    el.dispatchEvent(new KeyboardEvent('keydown', { bubbles: true }));
                    el.dispatchEvent(new Event('input', { bubbles: true }));
                    el.dispatchEvent(new Event('change', { bubbles: true }));
                    el.dispatchEvent(new KeyboardEvent('keyup', { bubbles: true }));
                    el.dispatchEvent(new KeyboardEvent('keypress', { bubbles: true }));
                    el.dispatchEvent(new Event('blur', { bubbles: true }));
                };

                const allInputs = Array.from(document.querySelectorAll('input, [contenteditable="true"]')).filter(visible);
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

                const singleCharInputs = otpLikeInputs.filter(input => input.maxLength === 1);
                if (singleCharInputs.length >= code.length)
                {
                    singleCharInputs.slice(0, code.length).forEach((input, index) => {
                        input.focus();
                        input.click?.();
                        setValue(input, code[index]);
                        dispatchInput(input);
                    });
                    return true;
                }

                const singleInput = otpLikeInputs.find(input => input.maxLength !== 1)
                    || allInputs.find(input => {
                        const attrs = normalize(`${input.id} ${input.name} ${input.placeholder} ${input.autocomplete} ${input.inputMode} ${input.type} ${input.className} ${input.getAttribute?.('aria-label') || ''}`);
                        return attrs.includes('otp')
                            || attrs.includes('code')
                            || attrs.includes('kod')
                            || attrs.includes('pin')
                            || attrs.includes('verify')
                            || attrs.includes('dogrulama')
                            || attrs.includes('sms');
                    });

                if (singleInput)
                {
                    singleInput.focus();
                    singleInput.click?.();
                    setValue(singleInput, code);
                    dispatchInput(singleInput);
                    return true;
                }

                const fallbackInput = allInputs.find(input => {
                    const type = normalize(input.type);
                    return type === 'tel' || type === 'text' || type === 'number';
                });

                if (fallbackInput)
                {
                    fallbackInput.focus();
                    fallbackInput.click?.();
                    setValue(fallbackInput, code);
                    dispatchInput(fallbackInput);
                    return true;
                }

                return false;
            }
            """,
            code);

        if (!filled)
            throw new InvalidOperationException("SMS doğrulama alanı bulunamadı.");

        await WaitAsync(700);

        try
        {
            var clicked = await page.EvaluateExpressionAsync<bool>(
                """
                (() => {
                    const applyButton = document.querySelector('button[data-cms-key="button_apply"]');
                    if (applyButton) {
                        const rect = applyButton.getBoundingClientRect();
                        const style = window.getComputedStyle(applyButton);
                        const visible = rect.width > 0 &&
                            rect.height > 0 &&
                            style.display !== 'none' &&
                            style.visibility !== 'hidden' &&
                            !applyButton.disabled;

                        if (visible) {
                            applyButton.scrollIntoView({ block: 'center', inline: 'center' });
                            ['pointerdown', 'mousedown', 'pointerup', 'mouseup', 'click'].forEach(type => {
                                applyButton.dispatchEvent(new MouseEvent(type, { bubbles: true, cancelable: true, view: window }));
                            });
                            applyButton.click();
                            return true;
                        }
                    }

                    const normalize = value => (value || '').replace(/\s+/g, ' ').trim().toLocaleLowerCase('tr-TR');
                    const visible = el => {
                        const rect = el.getBoundingClientRect();
                        const style = window.getComputedStyle(el);
                        return rect.width > 0 &&
                            rect.height > 0 &&
                            style.display !== 'none' &&
                            style.visibility !== 'hidden' &&
                            !el.disabled;
                    };

                    const exactTexts = ['doğrula', 'onayla', 'devam et', 'giriş yap', 'verify', 'continue'];
                    const partialTexts = ['doğrula', 'onay', 'devam', 'verify', 'continue'];

                    const candidates = Array.from(document.querySelectorAll('button, [role="button"], input[type="submit"], input[type="button"]'))
                        .filter(visible)
                        .map(element => {
                            const text = normalize(element.textContent || element.value || element.getAttribute('aria-label') || element.getAttribute('title') || '');
                            return { element, text };
                        })
                        .filter(item => item.text.length > 0);

                    const exactMatch = candidates.find(item => exactTexts.includes(item.text));
                    if (exactMatch) {
                        exactMatch.element.click();
                        return true;
                    }

                    const partialMatch = candidates.find(item => partialTexts.some(text => item.text.includes(text)));
                    if (partialMatch) {
                        partialMatch.element.click();
                        return true;
                    }

                    return false;
                })();
                """);

            Report(clicked
                ? "SMS doğrulama butonu tıklandı."
                : "SMS doğrulama butonu bulunamadı.");
        }
        catch
        {
        }
    }
}

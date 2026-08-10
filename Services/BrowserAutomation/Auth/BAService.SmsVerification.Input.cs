namespace Yolcu360Otomasyon.Services;

public sealed partial class BAService
{
    private Task<string?> FillSmsCodeInputAsync(string cleanCode)
    {
        var codeJson = ToJson(cleanCode);
        return EvaluateScriptAsync(
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
    }
}

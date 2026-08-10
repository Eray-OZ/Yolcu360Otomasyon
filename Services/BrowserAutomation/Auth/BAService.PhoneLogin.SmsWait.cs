namespace Yolcu360Otomasyon.Services;

public sealed partial class BAService
{
    private async Task WaitForSmsVerificationScreenAsync()
    {
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
}

namespace Yolcu360Otomasyon.Services;

public sealed partial class BAService
{
    private async Task WarmUpRecaptchaScoreAsync(int movementCount)
    {
        Report("Telefon numarası girildi, reCAPTCHA v3 güven puanı oluşturuluyor...");
        await DispatchHumanMouseMovementsAsync(movementCount);
    }

    private async Task ClickContinueButtonHumanLikeAsync()
    {
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
    }

    private async Task RetryContinueAfterRecaptchaIfNeededAsync()
    {
        var hasRecaptchaError = await EvaluateScriptAsync("window.__hasRecaptchaScoreError ? window.__hasRecaptchaScoreError() : false");
        if (!IsScriptTrue(hasRecaptchaError))
            return;

        Report("reCAPTCHA puan uyarısı algılandı (recaptcha_score_too_low). Insansı sayfa hareketleri artırılarak tekrar deneniyor...");
        await DispatchHumanMouseMovementsAsync(5);

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

    private async Task DispatchHumanMouseMovementsAsync(int movementCount)
    {
        for (var i = 0; i < movementCount; i++)
        {
            var rx = Random.Shared.Next(100, 500);
            var ry = Random.Shared.Next(100, 400);
            await EvaluateScriptAsync($"window.__dispatchHumanMousePath ? window.__dispatchHumanMousePath({rx}, {ry}) : null;");
            await Task.Delay(500);
        }
    }
}

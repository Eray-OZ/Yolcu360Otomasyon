namespace Yolcu360Otomasyon.Services;

public sealed partial class BAService
{
    private Task<string?> ClickSmsVerificationButtonAsync()
    {
        return EvaluateScriptAsync(
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
    }
}

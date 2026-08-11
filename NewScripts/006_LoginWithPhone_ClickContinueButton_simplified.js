// Source copy: JavaScriptCopies/006_BAService.Auth_LoginWithPhoneAsync_line163.js
// Purpose: Simplified alternative for clicking the "Devam" button.
//
// IMPORTANT:
// This version removes PointerEvent, MouseEvent, and form submit fallback calls.
// DevTools showed only a click listener on the button, so btn.click() may be enough.
// However, this is not guaranteed to behave the same in every login/CAPTCHA scenario.
// Test before moving this into the working C# automation code.

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

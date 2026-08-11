// Source copy: JavaScriptCopies/007_BAService.Auth_LoginWithPhoneAsync_line215.js
// Purpose: Retry click for the "Devam" button.
//
// Difference from original:
// - Uses the same selector strategy as script 006.
// - Also checks aria-label and [role="button"].
// - Removes disabled attribute/class, not only btn.disabled=false.
//
// This is intended as a consistency cleanup, not a behavior change.
// Test before moving this into the working C# automation code.

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

// Source copy: JavaScriptCopies/016_BAService.Auth_WaitForLoginCompletedAsync_line560.js
// Purpose: Detect whether Yolcu360 login completed.
//
// Based on observed logged-in localStorage values:
// - localStorage["user"] contains JSON with anonymous=false
// - localStorage["token"] contains JSON with accessToken
//
// Why this is safer than the original:
// URL/text checks only prove that the page changed.
// This checks whether real logged-in session data exists.
//
// Test before moving this into the working C# automation code.

(() => {
    try {
        const user = JSON.parse(localStorage.getItem('user') || 'null');
        const token = JSON.parse(localStorage.getItem('token') || 'null');

        return !!user &&
            user.anonymous === false &&
            !!token &&
            typeof token.accessToken === 'string' &&
            token.accessToken.length > 0;
    } catch {
        return false;
    }
})();

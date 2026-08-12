// Source copy: JavaScriptCopies/026_BAService.Payment_CompleteIyzicoSandboxPaymentAsync_line21.js
// Purpose: Wait until the iyzico card payment form exists.
//
// Based on captured iyzico sandbox payment HTML:
// - Card holder input: #ccname
// - Card number input: #ccnumber
// - Expiry input: #ccexp
// - CVC input: #cccvc
//
// This replaces the loose "any payment-like input exists" check.
// The form is considered ready only when all required card inputs exist.
//
// Test before moving this into the working C# automation code.

(() => {
    const selectors = ['#ccname', '#ccnumber', '#ccexp', '#cccvc'];
    return selectors.every(selector => !!document.querySelector(selector));
})();

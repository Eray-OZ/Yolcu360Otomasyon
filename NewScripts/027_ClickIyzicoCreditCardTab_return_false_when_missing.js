// Source copy: JavaScriptCopies/027_BAService.Payment_CompleteIyzicoSandboxPaymentAsync_line28.js
// Purpose: Select the iyzico credit card payment tab.
//
// Based on captured iyzico sandbox payment HTML:
// - Credit card tab has id="iyz-tab-credit-card"
//
// Difference from original:
// The original returns true even when the tab is missing.
// This version returns false when it cannot find the tab.
//
// Test before moving this into the working C# automation code.

(() => {
    const tab = document.querySelector('#iyz-tab-credit-card');
    if (!tab) return false;

    tab.click();
    return true;
})();

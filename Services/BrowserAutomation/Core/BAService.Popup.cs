namespace Yolcu360Otomasyon.Services;

public sealed partial class BAService
{
    public async Task<bool> CloseInitialPopupAsync()
    {
        var result = await EvaluateScriptAsync(
            """
            (() => {
                const closeButton = document.querySelector('.gs_trigger_discount_popup_close_container');
                if (!closeButton) return false;

                const rect = closeButton.getBoundingClientRect();
                const style = window.getComputedStyle(closeButton);
                const visible = rect.width > 0 &&
                    rect.height > 0 &&
                    style.visibility !== 'hidden' &&
                    style.display !== 'none';

                if (!visible) return false;

                closeButton.click();
                return true;
            })();
            """);

        return IsScriptTrue(result);
    }
}

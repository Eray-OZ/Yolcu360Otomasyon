namespace Yolcu360Otomasyon.Services;

public sealed partial class BAService
{
    private async Task ConfirmDatePickerAsync()
    {
        await EvaluateScriptAsync(
            """
            (() => {
                const selectBtn = document.querySelector('.dp__action_select, button.dp__action_select, .dp__select');
                if (selectBtn) {
                    selectBtn.click();
                    return true;
                }
                return false;
            })();
            """);
    }
}

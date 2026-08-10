namespace Yolcu360Otomasyon.Services;

public sealed partial class BAService
{
    private async Task WaitForDatePickerMenuAsync(TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            var menuVisible = await EvaluateScriptAsync(
                """
                (() => {
                    const menus = Array.from(document.querySelectorAll('.dp__menu, .dp__outer_menu_wrap, .dp__calendar'));
                    return menus.some(m => {
                        const rect = m.getBoundingClientRect();
                        const style = window.getComputedStyle(m);
                        return rect.width > 0 && rect.height > 0 && style.display !== 'none' && style.visibility !== 'hidden';
                    });
                })();
                """);

            if (IsScriptTrue(menuVisible))
                return;

            await Task.Delay(DatePickerMenuPollingDelay);
        }

        throw new TimeoutException("Tarih seçici takvim menüsü (dp__menu) görünmedi.");
    }
}

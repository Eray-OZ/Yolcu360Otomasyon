namespace Yolcu360Otomasyon.Services;

public sealed partial class BAService
{
    private async Task NavigateToMonthAsync(DateTime target)
    {
        for (var attempt = 0; attempt < 24; attempt++)
        {
            var headerText = await EvaluateScriptAsync(
                """
                (() => {
                    const menu = Array.from(document.querySelectorAll('.dp__menu, .dp__outer_menu_wrap'))
                        .find(m => {
                            const s = window.getComputedStyle(m);
                            const r = m.getBoundingClientRect();
                            return s.display !== 'none' && s.visibility !== 'hidden' && r.width > 0;
                        });
                    if (!menu) return '';
                    const headers = Array.from(menu.querySelectorAll('.dp__month_year_select, .dp__calendar_header_item, .dp__month_year_wrap, .dp__calendar_header'));
                    return headers.map(h => (h.textContent || '').trim()).join(' ');
                })();
                """);

            var currentText = (headerText ?? string.Empty).Trim('"');
            Report($"Takvim başlığı: '{currentText}' | Hedef: {target:MMMM yyyy}");

            if (IsTargetMonthVisible(currentText, target))
                return;

            var goBack = ShouldGoBack(currentText, target);
            var navSuccess = await ClickCalendarNavAsync(forward: !goBack);
            if (!navSuccess)
            {
                Report("Takvim yönlendirme butonuna tıklanamadı.");
                break;
            }

            await Task.Delay(CalendarNavigationDelay);
        }
    }
}

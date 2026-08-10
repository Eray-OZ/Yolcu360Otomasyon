namespace Yolcu360Otomasyon.Services;

public sealed partial class BAService
{
    public async Task SelectDateRangeAsync(DateTime pickupDate, DateTime returnDate)
    {
        Report($"Alış ve Bırakış tarihleri seçiliyor: {pickupDate:dd.MM.yyyy} – {returnDate:dd.MM.yyyy}");

        Report("Tarih seçici açılıyor...");
        var opened = await OpenDatePickerAsync();
        if (!opened)
            throw new InvalidOperationException("Tarih seçici (datepicker) açılamadı.");

        Report("Tarih takvimi bekleniyor...");
        await WaitForDatePickerMenuAsync(TimeSpan.FromSeconds(10));

        Report($"Alış tarihi için ay kontrol ediliyor: {pickupDate:MMMM yyyy}");
        await NavigateToMonthAsync(pickupDate);
        await Task.Delay(DatePickerActionDelay);

        Report($"Alış tarihi seçiliyor: {pickupDate:dd.MM.yyyy}");
        var pickupSelected = await ClickCalendarDayAsync(pickupDate);
        if (!pickupSelected)
            throw new InvalidOperationException($"Alış tarihi ({pickupDate:dd.MM.yyyy}) takvimde seçilemedi.");

        Report($"Alış tarihi seçildi: {pickupDate:dd.MM.yyyy}");
        await Task.Delay(DatePickerSelectionDelay);

        if (returnDate.Year != pickupDate.Year || returnDate.Month != pickupDate.Month)
        {
            Report($"Bırakış tarihi için ay geziliyor: {returnDate:MMMM yyyy}");
            await NavigateToMonthAsync(returnDate);
            await Task.Delay(DatePickerActionDelay);
        }

        Report($"Bırakış tarihi seçiliyor: {returnDate:dd.MM.yyyy}");
        var returnSelected = await ClickCalendarDayAsync(returnDate);
        if (!returnSelected)
            throw new InvalidOperationException($"Bırakış tarihi ({returnDate:dd.MM.yyyy}) takvimde seçilemedi.");

        Report($"Bırakış tarihi seçildi: {returnDate:dd.MM.yyyy}");
        await Task.Delay(DatePickerSelectionDelay);

        await ConfirmDatePickerAsync();
        await Task.Delay(DatePickerActionDelay);
    }

    private async Task<bool> OpenDatePickerAsync()
    {
        var datePickerSelectorJson = ToJson(DatePickerSelector);
        var dateTimeGroupSelectorJson = ToJson(DateTimeGroupSelector);

        var result = await EvaluateScriptAsync(
            $$"""
            (() => {
                const labelEl = Array.from(document.querySelectorAll('span, div, label, p'))
                    .find(el => {
                        const txt = (el.textContent || '').trim();
                        return txt === 'Alış Tarihi' || txt === 'Alış ve Bırakış Tarihi';
                    });
                const pickerFromLabel = labelEl?.closest('.dp__main, [modaltitle="Alış ve Bırakış Tarihi"], [modaltitlecmskey="pickup_and_dropoff_date"]');
                const pickerBySelector = document.querySelector({{datePickerSelectorJson}}) || document.querySelector({{dateTimeGroupSelectorJson}});

                const target = pickerFromLabel || pickerBySelector || labelEl;
                if (!target) return 'false';

                target.scrollIntoView({ block: 'center', inline: 'nearest' });
                const rect = target.getBoundingClientRect();
                const x = rect.left + rect.width / 2;
                const y = rect.top + rect.height / 2;

                const triggerEvents = (el) => {
                    if (!el) return;
                    const opts = { bubbles: true, cancelable: true, view: window, clientX: x, clientY: y };
                    if (typeof PointerEvent === 'function') {
                        el.dispatchEvent(new PointerEvent('pointerdown', { ...opts, pointerId: 1, pointerType: 'mouse', isPrimary: true, buttons: 1 }));
                        el.dispatchEvent(new PointerEvent('pointerup', { ...opts, pointerId: 1, pointerType: 'mouse', isPrimary: true }));
                    }
                    el.dispatchEvent(new MouseEvent('mousedown', { ...opts, buttons: 1 }));
                    el.dispatchEvent(new MouseEvent('mouseup', { ...opts }));
                    el.dispatchEvent(new MouseEvent('click', opts));
                    if (typeof el.click === 'function') el.click();
                };

                triggerEvents(target);
                const innerInput = target.querySelector('input, .dp__input, .dp__icon');
                if (innerInput && innerInput !== target) {
                    triggerEvents(innerInput);
                }

                return 'true';
            })();
            """);

        return IsScriptTrue(result);
    }

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

using System.Text.Json;
using PuppeteerSharp;
using Yolcu360Otomasyon.Models;

namespace Yolcu360Otomasyon.Services;

public sealed partial class BrowserAutomationService
{
    public async Task ApplySearchFiltersAndSearchAsync(SearchFilter filter)
    {
        var page = GetPage();

        Report("Yolcu360 ana sayfası açılıyor...");
        await page.GoToAsync(Yolcu360HomeUrl, WaitUntilNavigation.Networkidle2);
        await ShowDebugAsync("Sayfa açıldı.");

        Report("Sayfa etkileşime hazırlanıyor...");
        await WarmUpHydrationAsync();
        Report("Başlangıç popup'ı için bekleniyor...");
        await WaitAsync(2_500);
        await CloseInitialPopupAsync();

        Report($"Alış yeri yazılıyor: {filter.PickupLocation}");
        await FillPickupLocationAsync(filter.PickupLocation);

        Report($"Tarihler seçiliyor: {filter.PickupDate:dd.MM.yyyy} – {filter.ReturnDate:dd.MM.yyyy}");
        await SelectDateRangeAsync(filter.PickupDate, filter.ReturnDate);

        Report($"Alış saati seçiliyor: {filter.PickupTime}");
        await SelectTimeAsync(timePickerIndex: 0, filter.PickupTime);

        Report($"Bırakış saati seçiliyor: {filter.ReturnTime}");
        await SelectTimeAsync(timePickerIndex: 1, filter.ReturnTime);

        Report("Araç Ara butonuna tıklanıyor...");
        await ClickSearchButtonAsync();

        Report("Sonuç ekranı bekleniyor...");
        await WaitForSearchResultAsync();

        await ApplyResultPageFiltersAsync(filter);
    }

    private async Task FillPickupLocationAsync(string location)
    {
        var page = GetPage();

        if (string.IsNullOrWhiteSpace(location))
            throw new InvalidOperationException("Alış yeri boş bırakılamaz.");

        await page.WaitForSelectorAsync(Selectors.PickupLocationInput, new WaitForSelectorOptions
        {
            Visible = true,
            Timeout = 30_000
        });

        await page.FocusAsync(Selectors.PickupLocationInput);
        await page.EvaluateExpressionAsync("""
            (() => {
                const el = document.querySelector('#inputPickUpLocation');
                el.focus();
                el.select();
            })();
            """);

        await page.EvaluateExpressionAsync("""
            (() => {
                document.querySelectorAll('form').forEach(f => {
                    f.__y360_submitGuard = (e) => { e.preventDefault(); e.stopImmediatePropagation(); };
                    f.addEventListener('submit', f.__y360_submitGuard, true);
                });
                window.__y360_keyGuard = (e) => {
                    if (document.activeElement?.id === 'inputPickUpLocation' && e.key === 'Enter') {
                        e.preventDefault();
                        e.stopImmediatePropagation();
                    }
                };
                window.addEventListener('keydown', window.__y360_keyGuard, true);
            })();
            """);

        try
        {
            await page.Keyboard.TypeAsync(location, new PuppeteerSharp.Input.TypeOptions { Delay = 80 });

            if (!page.Url.Contains("yolcu360.com") || page.Url.Contains("search") || page.Url.Contains("arac-kiralama"))
            {
                await ShowDebugAsync("Sayfa yenilendi, anasayfaya dönülüyor...");
                await page.GoToAsync(Yolcu360HomeUrl, WaitUntilNavigation.Networkidle2);
                await WarmUpHydrationAsync();
                await WaitAsync(2_500);
                await CloseInitialPopupAsync();
                await page.WaitForSelectorAsync(Selectors.PickupLocationInput, new WaitForSelectorOptions
                {
                    Visible = true,
                    Timeout = 30_000
                });
                await page.FocusAsync(Selectors.PickupLocationInput);
                await page.Keyboard.TypeAsync(location, new PuppeteerSharp.Input.TypeOptions { Delay = 80 });
            }

            try
            {
                await page.WaitForFunctionAsync(
                    """
                    () => {
                        const items = Array.from(document.querySelectorAll('.search-autocomplete .location-item'));
                        return items.some(el => {
                            const rect = el.getBoundingClientRect();
                            const style = window.getComputedStyle(el);
                            return rect.width > 0 &&
                                rect.height > 0 &&
                                style.display !== 'none' &&
                                style.visibility !== 'hidden';
                        });
                    }
                    """,
                    new WaitForFunctionOptions { Timeout = 8_000 });
            }
            catch (WaitTaskTimeoutException)
            {
                await ShowDebugAsync("Alış yeri menüsü görünmedi, genel fallback deneniyor.");
                await WaitAsync(1_500);
            }

            var selectionApplied = false;

            for (var attempt = 1; attempt <= 3 && !selectionApplied; attempt++)
            {
                if (attempt > 1)
                {
                    await ShowDebugAsync($"Alış yeri click seçimi tekrar deneniyor. Deneme: {attempt}");
                    await page.FocusAsync(Selectors.PickupLocationInput);
                    await WaitAsync(400);
                }

                var locationJson = JsonSerializer.Serialize(location);
                var suggestionPoint = await page.EvaluateExpressionAsync<ClickPoint>($$"""
                    (() => {
                        const input = document.querySelector('#inputPickUpLocation');
                        if (!input) {
                            return { found: false, enabled: false, x: 0, y: 0, text: 'input yok' };
                        }

                        const inputRect = input.getBoundingClientRect();
                        const visible = el => {
                            const rect = el.getBoundingClientRect();
                            const style = window.getComputedStyle(el);
                            return rect.width > 0 &&
                                rect.height > 0 &&
                                style.display !== 'none' &&
                                style.visibility !== 'hidden';
                        };

                        const normalize = text => text
                            .toLocaleLowerCase('tr-TR')
                            .replace(/\s+/g, ' ')
                            .trim();

                        const getMainText = el => {
                            const firstBlock = el.querySelector('div > div:first-child');
                            return normalize(firstBlock?.textContent || '');
                        };

                        const getScore = (el, locationText) => {
                            const fullText = normalize(el.textContent || '');
                            const mainText = getMainText(el);

                            if (mainText === locationText) return 0;
                            if (fullText === locationText) return 1;
                            if (mainText.startsWith(locationText + ' ')) return 2;
                            if (fullText.startsWith(locationText + ' ')) return 3;
                            if (mainText.startsWith(locationText)) return 4;
                            if (fullText.startsWith(locationText)) return 5;
                            if (mainText.includes(locationText)) return 6;
                            if (fullText.includes(locationText)) return 7;
                            return 8;
                        };

                        const locationText = normalize({{locationJson}});

                        const allItems = Array.from(document.querySelectorAll('.search-autocomplete .location-item'));
                        const candidates = allItems
                            .filter(el => {
                                if (!visible(el)) return false;
                                if (el === input || el.contains(input)) return false;

                                const rect = el.getBoundingClientRect();
                                const text = (el.textContent || '').trim();

                                return text.length > 1 &&
                                    rect.top >= inputRect.bottom - 8 &&
                                    rect.left < inputRect.right + 500 &&
                                    rect.right > inputRect.left;
                            })
                            .sort((a, b) => {
                                const aScore = getScore(a, locationText);
                                const bScore = getScore(b, locationText);
                                if (aScore != bScore) return aScore - bScore;
                                const ar = a.getBoundingClientRect();
                                const br = b.getBoundingClientRect();
                                return ar.top === br.top ? ar.left - br.left : ar.top - br.top;
                            });

                        const target = candidates[0];
                        if (!target) {
                            return { found: false, enabled: false, x: 0, y: 0, text: 'öneri bulunamadı' };
                        }

                        target.scrollIntoView({ block: 'center', inline: 'nearest' });
                        const rect = target.getBoundingClientRect();

                        return {
                            found: true,
                            enabled: true,
                            x: rect.left + rect.width / 2,
                            y: rect.top + rect.height / 2,
                            text: (target.textContent || '').trim(),
                            index: allItems.indexOf(target)
                        };
                    })();
                    """);

                if (!suggestionPoint.Found)
                    throw new InvalidOperationException($"Alış yeri önerisi bulunamadı. Yazılan değer: {location}");

                await WaitAsync(900);
                await page.Mouse.MoveAsync(suggestionPoint.X, suggestionPoint.Y);
                await WaitAsync(150);
                await page.Mouse.ClickAsync(suggestionPoint.X, suggestionPoint.Y);
                await WaitAsync(700);

                try
                {
                    await page.WaitForFunctionAsync(
                        """
                        () => {
                            const input = document.querySelector('#inputPickUpLocation');
                            const menu = document.querySelector('.search-autocomplete');
                            const menuVisible = !!menu && menu.getBoundingClientRect().height > 0;
                            return !!input && input.value.trim().length > 0 && !menuVisible;
                        }
                        """,
                        new WaitForFunctionOptions { Timeout = 2_000 });
                    selectionApplied = true;
                }
                catch (WaitTaskTimeoutException)
                {
                    await ShowDebugAsync("Öneri click sonrası seçim henüz uygulanmadı.");
                }
            }

            if (!selectionApplied)
                throw new InvalidOperationException($"Alış yeri önerisi seçilemedi. Yazılan değer: {location}");
        }
        finally
        {
            await page.EvaluateExpressionAsync("""
                (() => {
                    document.querySelectorAll('form').forEach(f => {
                        if (f.__y360_submitGuard) {
                            f.removeEventListener('submit', f.__y360_submitGuard, true);
                            delete f.__y360_submitGuard;
                        }
                    });
                    if (window.__y360_keyGuard) {
                        window.removeEventListener('keydown', window.__y360_keyGuard, true);
                        delete window.__y360_keyGuard;
                    }
                })();
                """);
        }

        try
        {
            await page.WaitForFunctionAsync(
                """
                () => {
                    const input = document.querySelector('#inputPickUpLocation');
                    const menu = document.querySelector('.search-autocomplete');
                    const menuVisible = !!menu && menu.getBoundingClientRect().height > 0;
                    return !!input && input.value.trim().length > 0 && !menuVisible;
                }
                """,
                new WaitForFunctionOptions { Timeout = 3_000 });
        }
        catch (WaitTaskTimeoutException)
        {
            await ShowDebugAsync("Alış yeri menüsü kapanmadı; input değeri kontrol ediliyor.");
        }

        await WaitAsync(800);
        var value = await page.EvaluateExpressionAsync<string>(
            "document.querySelector('#inputPickUpLocation')?.value || ''");

        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Alış yeri '{location}' girilemedi. Autocomplete listesinden geçerli bir konum seçilmesi gerekiyor.");

        Report($"Alış yeri seçildi: {value}");
        await ShowDebugAsync($"Alış yeri: {value}");

        await page.Keyboard.PressAsync("Escape");
        await page.EvaluateExpressionAsync("document.activeElement?.blur();");
        await WaitAsync(300);
    }

    private async Task SelectDateRangeAsync(DateTime pickupDate, DateTime returnDate)
    {
        var page = GetPage();

        var pickerPoint = await page.EvaluateExpressionAsync<ClickPoint>("""
            (() => {
                const labelEl = Array.from(document.querySelectorAll('span, div'))
                    .find(el => el.textContent.trim() === 'Alış Tarihi');
                const picker = labelEl?.closest('.dp__main.dp__theme_light');
                if (!picker) return { found: false, enabled: false, x: 0, y: 0, text: '' };
                picker.scrollIntoView({ block: 'center', inline: 'center' });
                const rect = picker.getBoundingClientRect();
                return { found: true, enabled: rect.width > 0 && rect.height > 0,
                         x: rect.left + rect.width / 2, y: rect.top + rect.height / 2, text: '' };
            })();
            """);

        if (!pickerPoint.Found || !pickerPoint.Enabled)
            throw new InvalidOperationException("Alış Tarihi picker bulunamadı.");

        await page.Mouse.ClickAsync(pickerPoint.X, pickerPoint.Y);

        await page.WaitForSelectorAsync(Selectors.DatePickerMenu, new WaitForSelectorOptions
        {
            Visible = true,
            Timeout = 10_000
        });
        await ShowDebugAsync($"Takvim açıldı. Hedef: {pickupDate:dd.MM.yyyy} – {returnDate:dd.MM.yyyy}");

        await NavigateToMonthAsync(pickupDate);

        var pickupSelected = await ClickCalendarDayAsync(pickupDate);
        if (!pickupSelected)
            throw new InvalidOperationException($"Alış tarihi {pickupDate:dd.MM.yyyy} seçilemedi.");

        Report($"Alış tarihi seçildi: {pickupDate:dd.MM.yyyy}");
        await ShowDebugAsync($"Alış tarihi seçildi: {pickupDate:dd.MM.yyyy}");
        await WaitAsync(400);

        if (returnDate.Year != pickupDate.Year || returnDate.Month != pickupDate.Month)
        {
            await NavigateToMonthAsync(returnDate);
            await WaitAsync(300);
        }

        var returnSelected = await ClickCalendarDayAsync(returnDate);
        if (!returnSelected)
            throw new InvalidOperationException($"Bırakış tarihi {returnDate:dd.MM.yyyy} seçilemedi.");

        Report($"Bırakış tarihi seçildi: {returnDate:dd.MM.yyyy}");
        await ShowDebugAsync($"Bırakış tarihi seçildi: {returnDate:dd.MM.yyyy}");
        await WaitAsync(500);
    }

    private async Task<bool> ClickCalendarDayAsync(DateTime date)
    {
        var page = GetPage();
        var dayJson = JsonSerializer.Serialize(date.Day);
        var monthJson = JsonSerializer.Serialize(
            new[] { "Ocak","Şubat","Mart","Nisan","Mayıs","Haziran","Temmuz","Ağustos","Eylül","Ekim","Kasım","Aralık" }[date.Month - 1]);
        var yearJson = JsonSerializer.Serialize(date.Year.ToString());

        return await page.EvaluateExpressionAsync<bool>($$"""
            (() => {
                const menu = Array.from(document.querySelectorAll('.dp__menu'))
                    .find(m => window.getComputedStyle(m).display !== 'none' && m.getBoundingClientRect().width > 0);
                if (!menu) return false;

                const dayTarget   = {{dayJson}};
                const monthTarget = {{monthJson}};
                const yearTarget  = {{yearJson}};

                const allCalendars = Array.from(menu.querySelectorAll('.dp__calendar'));
                let searchRoot = allCalendars.length > 0 ? null : menu;

                for (const cal of allCalendars) {
                    const hdr = cal.querySelector('.dp__month_year_select');
                    const hdrText = hdr?.textContent?.trim() ?? '';
                    if (hdrText.includes(monthTarget) && hdrText.includes(yearTarget)) {
                        searchRoot = cal;
                        break;
                    }
                }

                if (!searchRoot) searchRoot = menu;

                const selectors = [
                    '.dp__cell_inner',
                    '.dp__calendar_item button',
                    '.dp__calendar_item > div',
                    '.dp__calendar_item',
                ];

                for (const sel of selectors) {
                    const candidates = Array.from(searchRoot.querySelectorAll(sel))
                        .filter(c => {
                            const text = c.textContent.trim();
                            const num = parseInt(text, 10);
                            if (!text || isNaN(num)) return false;
                            const item = c.closest('.dp__calendar_item') ?? c;
                            return !item.classList.contains('dp__cell_offset') &&
                                   !c.classList.contains('dp__cell_offset');
                        });

                    const cell = candidates.find(c => parseInt(c.textContent.trim(), 10) === dayTarget);
                    if (cell) {
                        cell.scrollIntoView({ block: 'nearest' });
                        cell.click();
                        return true;
                    }
                }

                return false;
            })();
            """);
    }

    private async Task NavigateToMonthAsync(DateTime target)
    {
        var page = GetPage();

        for (var attempt = 0; attempt < 24; attempt++)
        {
            var currentText = await page.EvaluateExpressionAsync<string>($$"""
                (() => {
                    const menu = Array.from(document.querySelectorAll('.dp__menu'))
                        .find(m => {
                            const s = window.getComputedStyle(m);
                            const r = m.getBoundingClientRect();
                            return s.display !== 'none' && s.visibility !== 'hidden' && r.width > 0;
                        });
                    if (!menu) return '';
                    const headers = Array.from(menu.querySelectorAll('.dp__month_year_select'));
                    return headers.map(h => h.textContent.trim()).join(' ');
                })();
                """);

            if (IsTargetMonthVisible(currentText, target))
                break;

            if (ShouldGoBack(currentText, target))
                await ClickCalendarNavAsync(forward: false);
            else
                await ClickCalendarNavAsync(forward: true);

            await WaitAsync(300);
        }
    }

    private async Task ClickCalendarNavAsync(bool forward)
    {
        var page = GetPage();

        var clicked = await page.EvaluateExpressionAsync<bool>($$"""
            (() => {
                const next = document.querySelector("[data-dp-element='action-next']");
                const prev = document.querySelector("[data-dp-element='action-prev']");
                const btn = {{(forward ? "next" : "prev")}};
                if (btn) { btn.click(); return true; }

                const navBtns = Array.from(document.querySelectorAll('.dp__nav_btn'));
                const target = {{(forward ? "navBtns[navBtns.length - 1]" : "navBtns[0]")}};
                if (target) { target.click(); return true; }

                return false;
            })();
            """);

        if (!clicked)
            await ShowDebugAsync("Takvim navigasyon butonu bulunamadı.");
    }

    private async Task SelectTimeAsync(int timePickerIndex, string time)
    {
        var page = GetPage();

        if (string.IsNullOrWhiteSpace(time))
            return;

        var timeJson = JsonSerializer.Serialize(time.Trim());
        var indexJson = JsonSerializer.Serialize(timePickerIndex);

        var opened = await page.EvaluateExpressionAsync<bool>($$"""
            (() => {
                const groups = document.querySelectorAll('[modaltitle="Alış ve Bırakış Tarihi"]');
                const group = groups[{{indexJson}}];
                if (!group) return false;

                const timeBox = group.querySelectorAll(':scope > div')[1];
                if (!timeBox) return false;

                timeBox.click();
                return true;
            })();
            """);

        if (!opened)
        {
            await ShowDebugAsync($"Saat picker[{timePickerIndex}] açılamadı, atlanıyor.");
            return;
        }

        await WaitAsync(500);

        var selected = await page.EvaluateExpressionAsync<bool>($$"""
            (() => {
                const target = {{timeJson}};
                const options = Array.from(document.querySelectorAll('.dropdown-item, [role="option"], li, .time-option'));

                const found = options.find(o =>
                    o.textContent.trim() === target ||
                    o.textContent.trim().startsWith(target));

                if (found) { found.click(); return true; }

                const all = Array.from(document.querySelectorAll('div, li, span, button'))
                    .filter(el => {
                        const t = el.textContent.trim();
                        return (t === target || t.startsWith(target)) && el.children.length === 0;
                    });

                if (all.length > 0) { all[0].click(); return true; }
                return false;
            })();
            """);

        if (!selected)
            await ShowDebugAsync($"Saat '{time}' dropdown'da bulunamadı, varsayılan bırakıldı.");
        else
            await ShowDebugAsync($"Saat seçildi: {time}");

        await WaitAsync(300);
    }

    private async Task ClickSearchButtonAsync()
    {
        var page = GetPage();

        await page.WaitForSelectorAsync(Selectors.SearchButton, new WaitForSelectorOptions
        {
            Visible = true,
            Timeout = 30_000
        });

        await page.Keyboard.PressAsync("Escape");
        await WaitAsync(250);

        var clickPoint = await page.EvaluateExpressionAsync<ClickPoint>("""
            (() => {
                const btn = document.querySelector('#search');
                if (!btn) {
                    return { found: false, enabled: false, x: 0, y: 0, text: '' };
                }

                btn.scrollIntoView({ block: 'center', inline: 'center' });

                const rect = btn.getBoundingClientRect();
                const style = window.getComputedStyle(btn);
                const enabled =
                    !btn.disabled &&
                    btn.getAttribute('aria-disabled') !== 'true' &&
                    style.pointerEvents !== 'none' &&
                    rect.width > 0 &&
                    rect.height > 0;

                return {
                    found: true,
                    enabled,
                    x: rect.left + rect.width / 2,
                    y: rect.top + rect.height / 2,
                    text: btn.textContent.trim()
                };
            })();
            """);

        if (!clickPoint.Found)
            throw new InvalidOperationException("Araç Ara butonu DOM'da bulunamadı.");

        if (!clickPoint.Enabled)
            throw new InvalidOperationException($"Araç Ara butonu pasif görünüyor. Buton yazısı: {clickPoint.Text}");

        var beforeUrl = page.Url;

        await page.Mouse.ClickAsync(clickPoint.X, clickPoint.Y);
        await WaitAsync(750);

        var changedAfterMouseClick = await HasSearchStartedAsync(beforeUrl);

        if (!changedAfterMouseClick)
        {
            await page.EvaluateExpressionAsync("""
                (() => {
                    const btn = document.querySelector('#search');
                    if (!btn) return;

                    btn.scrollIntoView({ block: 'center', inline: 'center' });
                    btn.focus();

                    btn.dispatchEvent(new PointerEvent('pointerdown', {
                        bubbles: true,
                        cancelable: true,
                        pointerType: 'mouse',
                        isPrimary: true
                    }));
                    btn.dispatchEvent(new MouseEvent('mousedown', {
                        bubbles: true,
                        cancelable: true,
                        view: window
                    }));
                    btn.dispatchEvent(new PointerEvent('pointerup', {
                        bubbles: true,
                        cancelable: true,
                        pointerType: 'mouse',
                        isPrimary: true
                    }));
                    btn.dispatchEvent(new MouseEvent('mouseup', {
                        bubbles: true,
                        cancelable: true,
                        view: window
                    }));
                    btn.dispatchEvent(new MouseEvent('click', {
                        bubbles: true,
                        cancelable: true,
                        view: window
                    }));
                    btn.click();
                })();
                """);

            await WaitAsync(750);
        }

        if (!await HasSearchStartedAsync(beforeUrl))
        {
            var diag = await GetDiagnosticAsync();
            throw new InvalidOperationException($"Araç Ara butonu tıklandı ama sayfa aramayı başlatmadı. {diag}");
        }

        Report("Araç Ara butonu tıklandı.");
        await ShowDebugAsync("Araç Ara butonu tıklandı.");
    }

    private async Task<bool> HasSearchStartedAsync(string beforeUrl)
    {
        var page = GetPage();

        return await page.EvaluateExpressionAsync<bool>($$"""
            (() => {
                const beforeUrl = {{JsonSerializer.Serialize(beforeUrl)}};
                const text = document.body.innerText.toLocaleLowerCase('tr-TR');

                return window.location.href !== beforeUrl
                    || window.location.href.includes('search')
                    || window.location.href.includes('arac-kiralama')
                    || window.location.href.includes('list')
                    || text.includes('sonuç')
                    || text.includes('araç bulundu')
                    || text.includes('kirala')
                    || text.includes('lütfen bir alış yeri seçin');
            })();
            """);
    }

    private async Task WaitForSearchResultAsync()
    {
        var page = GetPage();

        try
        {
            await page.WaitForFunctionAsync(
                """
                () => {
                    const text = document.body.innerText.toLocaleLowerCase('tr-TR');
                    return window.location.href.includes('search')
                        || window.location.href.includes('arac-kiralama')
                        || window.location.href.includes('list')
                        || text.includes('tl')
                        || text.includes('araç bulundu')
                        || text.includes('kirala')
                        || text.includes('lütfen bir alış yeri seçin');
                }
                """,
                new WaitForFunctionOptions { Timeout = 20_000 });
        }
        catch (WaitTaskTimeoutException)
        {
            var diag = await GetDiagnosticAsync();
            throw new InvalidOperationException($"Arama tetiklendi ama sonuç gelmedi. {diag}");
        }

        var bodyText = await GetBodyTextAsync();

        if (bodyText.Contains("Lütfen bir alış yeri seçin", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "Yolcu360, alış yeri seçimini geçersiz saydı. " +
                "Autocomplete listesinden kayıtlı bir konum seçilmesi gerekiyor.");
    }

    private static bool IsTargetMonthVisible(string headerText, DateTime target)
    {
        if (string.IsNullOrWhiteSpace(headerText))
            return false;

        var turkishMonths = new[]
        {
            "Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran",
            "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık"
        };

        var monthName = turkishMonths[target.Month - 1];
        var yearStr = target.Year.ToString();

        return headerText.Contains(monthName, StringComparison.OrdinalIgnoreCase)
            && headerText.Contains(yearStr);
    }

    private static bool ShouldGoBack(string headerText, DateTime target)
    {
        if (string.IsNullOrWhiteSpace(headerText))
            return false;

        foreach (var part in headerText.Split(' '))
        {
            if (int.TryParse(part, out var year))
            {
                if (year > target.Year) return true;
                if (year < target.Year) return false;
                break;
            }
        }

        var turkishMonths = new[]
        {
            "Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran",
            "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık"
        };

        for (var i = 0; i < turkishMonths.Length; i++)
        {
            if (headerText.Contains(turkishMonths[i], StringComparison.OrdinalIgnoreCase))
                return (i + 1) > target.Month;
        }

        return false;
    }
}

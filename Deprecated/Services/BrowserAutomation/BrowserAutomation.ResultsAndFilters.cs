using System.Text.Json;
using PuppeteerSharp;
using Yolcu360Otomasyon.Models;

namespace Yolcu360Otomasyon.Services;

public sealed partial class BrowserAutomationService
{
    public async Task<IReadOnlyList<SearchResultItem>> ReadSearchResultsAsync()
    {
        var page = GetPage();

        Report("Sonuç kartları bekleniyor...");

        try
        {
            await page.WaitForFunctionAsync(
                """
                () => {
                    const cards = document.querySelectorAll('#car_card_list .car-card, .car-card, .py-2.car-card');
                    const bodyText = (document.body.innerText || '').toLocaleLowerCase('tr-TR');
                    return cards.length > 0
                        || bodyText.includes('araç bulundu')
                        || bodyText.includes('hemen kirala')
                        || bodyText.includes('günlük fiyat');
                }
                """,
                new WaitForFunctionOptions { Timeout = 30_000 });
        }
        catch (WaitTaskTimeoutException)
        {
            var diag = await GetDiagnosticAsync();
            throw new InvalidOperationException($"Sonuç kartları yüklenmedi. {diag}");
        }

        await StabilizeResultsPageAsync();

        Report("Sonuçlar okunuyor...");

        var results = await page.EvaluateFunctionAsync<SearchResultItem[]>(
            """
            () => {
                const normalize = value => (value || '').replace(/\s+/g, ' ').trim();

                const cards = Array.from(document.querySelectorAll('#car_card_list .car-card, .car-card, .py-2.car-card'))
                    .filter(card => {
                        const rect = card.getBoundingClientRect();
                        const style = window.getComputedStyle(card);
                        return rect.width > 0 &&
                            rect.height > 0 &&
                            style.display !== 'none' &&
                            style.visibility !== 'hidden';
                    });

                return cards
                    .map(card => {
                        const specs = Array.from(card.querySelectorAll('.icon-gear-type, .icon-gas-type'))
                            .map(icon => normalize(icon.parentElement?.textContent))
                            .filter(Boolean);

                        const title = normalize(card.querySelector('.text-dark-gray.text-lg.font-bold')?.textContent);
                        const subtitle = normalize(card.querySelector('[data-cms-key="or_similar"]')?.textContent);
                        const price = normalize(card.querySelector('#car_total_price')?.textContent);
                        const dailyPrice = normalize(card.querySelector('[data-cms-key="text_daily_price2"]')?.textContent);
                        const transmission = specs.find(text => /manuel|otomatik/i.test(text)) || '';
                        const fuelType = specs.find(text => /benzin|dizel|hibrit|elektrik/i.test(text)) || '';
                        const supplier = normalize(card.querySelector('figure img[alt]')?.getAttribute('alt'));
                        const pickupInfo = normalize(card.querySelector('.icon-filled')?.parentElement?.textContent);
                        const actionText = normalize(card.querySelector('[data-cms-key="button_rent_now"]')?.textContent)
                            || normalize(card.querySelector('button')?.textContent);
                        const url = normalize(card.querySelector('a[href]')?.getAttribute('href'));

                        return {
                            title,
                            subtitle,
                            price,
                            dailyPrice,
                            transmission,
                            fuelType,
                            supplier,
                            pickupInfo,
                            actionText,
                            url
                        };
                    })
                    .filter(item => item.title || item.price);
            }
            """);

        Report($"{results.Length} sonuç okundu.");
        return results;
    }

    private async Task StabilizeResultsPageAsync()
    {
        var page = GetPage();

        await WaitAsync(2_500);
        await page.Mouse.WheelAsync(0, 500);
        await WaitAsync(900);
        await page.Mouse.WheelAsync(0, -300);
        await WaitAsync(1_200);

        try
        {
            await page.WaitForFunctionAsync(
                """
                () => {
                    const cards = document.querySelectorAll('#car_card_list .car-card, .car-card, .py-2.car-card');
                    return cards.length > 0;
                }
                """,
                new WaitForFunctionOptions { Timeout = 10_000 });
        }
        catch
        {
        }
    }

    private async Task ApplyResultPageFiltersAsync(SearchFilter filter)
    {
        Report("Sonuç sayfası açıldı, filtre paneli hazırlanıyor...");
        await ScrollToFiltersAsync();
        await WaitAsync(1_500);

        var transmissionApplied = await ApplyTransmissionFilterAsync(filter.TransmissionType);
        var fuelApplied = await ApplyFuelFilterAsync(filter.FuelType);

        if (transmissionApplied || fuelApplied)
        {
            Report("Sonuç sayfası filtreleri uygulanıyor...");
            await WaitForFilteredResultsRefreshAsync();
        }
    }

    private async Task<bool> ApplyTransmissionFilterAsync(string transmissionType)
    {
        return transmissionType.Trim().ToLowerInvariant() switch
        {
            "automatic" or "otomatik" => await ClickResultFilterOptionAsync("Vites filtresi", "filter-transmission", ["otomatik"]),
            "manual" or "manuel" => await ClickResultFilterOptionAsync("Vites filtresi", "filter-transmission", ["manuel"]),
            _ => false
        };
    }

    private async Task<bool> ApplyFuelFilterAsync(string fuelType)
    {
        return fuelType.Trim().ToLowerInvariant() switch
        {
            "diesel" or "dizel" => await ClickResultFilterOptionAsync("Yakıt filtresi", "filter-fuel", ["dizel", "benzin/dizel"]),
            "gasoline" or "benzin" => await ClickResultFilterOptionAsync("Yakıt filtresi", "filter-fuel", ["benzin", "benzin/dizel"]),
            _ => false
        };
    }

    private async Task<bool> ClickResultFilterOptionAsync(string filterName, string filterPrefix, IReadOnlyList<string> targetTexts)
    {
        var page = GetPage();
        var prefixJson = JsonSerializer.Serialize(filterPrefix);
        Report($"{filterName} hazırlanıyor...");

        try
        {
            await page.WaitForFunctionAsync(
                $$"""
                () => document.querySelectorAll(`label[name^="${{{prefixJson}}}."]`).length > 0
                """,
                new WaitForFunctionOptions { Timeout = 10_000 });
        }
        catch (WaitTaskTimeoutException)
        {
            Report($"{filterName} bulunamadı.");
            return false;
        }

        var appliedFilter = await page.EvaluateFunctionAsync<AppliedFilterResult>(
            $$"""
            (targets, prefix) => {
                const normalize = value => (value || '')
                    .toLocaleLowerCase('tr-TR')
                    .replace(/\s+/g, ' ')
                    .trim();

                const visible = el => {
                    const rect = el.getBoundingClientRect();
                    const style = window.getComputedStyle(el);
                    return rect.width > 0 &&
                        rect.height > 0 &&
                        style.display !== 'none' &&
                        style.visibility !== 'hidden';
                };

                const normalizedTargets = targets.map(normalize);

                const labels = Array.from(document.querySelectorAll(`label[name^="${prefix}."]`))
                    .filter(visible);

                const score = text => {
                    if (normalizedTargets.includes(text)) return 0;
                    if (normalizedTargets.some(target => text.startsWith(target + ' '))) return 1;
                    if (normalizedTargets.some(target => text.includes(target))) return 2;
                    return 3;
                };

                const candidates = labels
                    .map(label => ({
                        label,
                        text: normalize(label.textContent || ''),
                        checkbox: label.querySelector('input[type="checkbox"], input[type="radio"]')
                    }))
                    .filter(item => item.text.length > 0)
                    .sort((a, b) => score(a.text) - score(b.text));

                const target = candidates.find(item => score(item.text) < 3);
                if (!target) return { applied: false, text: '' };

                target.label.scrollIntoView({ block: 'center', inline: 'nearest' });

                ['pointerdown', 'mousedown', 'pointerup', 'mouseup', 'click'].forEach(type => {
                    target.label.dispatchEvent(new MouseEvent(type, { bubbles: true, cancelable: true, view: window }));
                });
                target.label.click();

                if (target.checkbox && !target.checkbox.checked) {
                    target.checkbox.click();
                }

                return {
                    applied: !!target.checkbox?.checked,
                    text: target.text
                };
            }
            """,
            targetTexts,
            filterPrefix);

        if (!appliedFilter.Applied)
        {
            Report($"{filterName} seçilemedi.");
            return false;
        }

        Report($"{filterName} seçildi: {appliedFilter.Text}");
        await WaitAsync(2_000);

        return true;
    }

    private async Task ScrollToFiltersAsync()
    {
        var page = GetPage();
        await page.EvaluateExpressionAsync(
            """
            (() => {
                const panel = document.querySelector('#stickyFilterCard');
                panel?.scrollIntoView({ block: 'start', inline: 'nearest' });
                window.scrollBy(0, -40);
            })();
            """);
    }

    private async Task WaitForFilteredResultsRefreshAsync()
    {
        var page = GetPage();

        try
        {
            await page.WaitForFunctionAsync(
                """
                () => {
                    const cards = document.querySelectorAll('#car_card_list .car-card, .car-card');
                    return cards.length > 0;
                }
                """,
                new WaitForFunctionOptions { Timeout = 15_000 });
        }
        catch (WaitTaskTimeoutException)
        {
            var diag = await GetDiagnosticAsync();
            throw new InvalidOperationException($"Filtreleme sonrası sonuçlar güncellenmedi. {diag}");
        }

        await WaitAsync(2_000);
    }
}

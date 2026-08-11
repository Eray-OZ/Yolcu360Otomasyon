using System.Text.Json;
using Yolcu360Otomasyon.Models;

namespace Yolcu360Otomasyon.Services;

public sealed partial class EmbeddedBrowserAutomationService
{
    public async Task ApplyResultFiltersAsync(SearchFilter filter)
    {
        if (filter is null) return;

        var hasTransmission = !string.IsNullOrWhiteSpace(filter.TransmissionType);
        var hasFuel = !string.IsNullOrWhiteSpace(filter.FuelType);

        if (!hasTransmission && !hasFuel) return;

        Report($"Gömülü tarayıcıda filtreler uygulanıyor (Vites: {filter.TransmissionType}, Yakıt: {filter.FuelType})...");
        await WaitForResultFiltersReadyAsync(TimeSpan.FromSeconds(8));

        if (hasTransmission)
        {
            var transmissionNorm = filter.TransmissionType.Trim().ToLowerInvariant();
            var targetTexts = transmissionNorm switch
            {
                "otomatik" or "automatic" => new[] { "otomatik" },
                "manuel" or "manual" => new[] { "manuel" },
                _ => Array.Empty<string>()
            };

            if (targetTexts.Length > 0)
            {
                await ClickFilterOptionAsync("Vites filtresi", "filter-transmission", targetTexts);
                await WaitForResultFiltersReadyAsync(TimeSpan.FromSeconds(5));
            }
        }

        if (hasFuel)
        {
            var fuelNorm = filter.FuelType.Trim().ToLowerInvariant();
            var targetTexts = fuelNorm switch
            {
                "dizel" or "diesel" => new[] { "dizel", "benzin/dizel" },
                "benzin" or "gasoline" => new[] { "benzin", "benzin/dizel" },
                _ => Array.Empty<string>()
            };

            if (targetTexts.Length > 0)
            {
                await ClickFilterOptionAsync("Yakıt filtresi", "filter-fuel", targetTexts);
                await WaitForResultFiltersReadyAsync(TimeSpan.FromSeconds(5));
            }
        }

        Report("Filtreler uygulandı, sonuçların yenilenmesi bekleniyor...");
        await WaitForSearchResultsAsync();
    }

    private Task<bool> WaitForResultFiltersReadyAsync(TimeSpan timeout)
    {
        return WaitForScriptTrueOrTimeoutAsync(
            """
            (() => {
                const visible = el => {
                    const rect = el.getBoundingClientRect();
                    const style = window.getComputedStyle(el);
                    return rect.width > 0 && rect.height > 0 && style.display !== 'none' && style.visibility !== 'hidden';
                };

                const filterInputs = Array.from(document.querySelectorAll('label[name^="filter-"], input[name^="filter-"], input[type="checkbox"], input[type="radio"]'))
                    .filter(visible);

                const bodyText = (document.body.innerText || '').toLocaleLowerCase('tr-TR');
                return filterInputs.length > 0 ||
                    bodyText.includes('vites') ||
                    bodyText.includes('yakıt') ||
                    bodyText.includes('filtre');
            })();
            """,
            timeout);
    }

    private async Task<bool> ClickFilterOptionAsync(string filterName, string filterPrefix, string[] targetTexts)
    {
        var targetTextsJson = JsonSerializer.Serialize(targetTexts);
        var filterPrefixJson = JsonSerializer.Serialize(filterPrefix);

        Report($"{filterName} aranıyor ({string.Join(", ", targetTexts)})...");

        var success = await EvaluateBooleanScriptAsync(
            $$"""
            (() => {
                const targets = {{targetTextsJson}};
                const prefix = {{filterPrefixJson}};

                const normalize = value => (value || '')
                    .toLocaleLowerCase('tr-TR')
                    .replace(/\s+/g, ' ')
                    .trim();

                const visible = el => {
                    const rect = el.getBoundingClientRect();
                    const style = window.getComputedStyle(el);
                    return rect.width > 0 && rect.height > 0 && style.display !== 'none' && style.visibility !== 'hidden';
                };

                const normalizedTargets = targets.map(normalize);

                let labels = Array.from(document.querySelectorAll(`label[name^="${prefix}."], input[name^="${prefix}."]`)).filter(visible);

                if (labels.length === 0) {
                    labels = Array.from(document.querySelectorAll('label, input[type="checkbox"], input[type="radio"]')).filter(visible);
                }

                const score = text => {
                    if (normalizedTargets.includes(text)) return 0;
                    if (normalizedTargets.some(target => text.startsWith(target + ' '))) return 1;
                    if (normalizedTargets.some(target => text.includes(target))) return 2;
                    return 3;
                };

                const candidates = labels
                    .map(el => {
                        const text = normalize(el.textContent || el.value || el.getAttribute('aria-label') || '');
                        return { el, text };
                    })
                    .filter(item => item.text.length > 0)
                    .sort((a, b) => score(a.text) - score(b.text));

                const match = candidates.find(item => score(item.text) < 3);
                if (!match) return false;

                const targetEl = match.el;
                targetEl.scrollIntoView({ block: 'center', inline: 'nearest' });

                ['pointerdown', 'mousedown', 'pointerup', 'mouseup', 'click'].forEach(type => {
                    targetEl.dispatchEvent(new MouseEvent(type, { bubbles: true, cancelable: true, view: window }));
                });
                targetEl.click();

                const checkbox = targetEl.querySelector?.('input[type="checkbox"], input[type="radio"]') || (targetEl.tagName === 'INPUT' ? targetEl : null);
                if (checkbox && !checkbox.checked) {
                    checkbox.click();
                    checkbox.dispatchEvent(new Event('change', { bubbles: true }));
                }

                return true;
            })();
            """);

        Report(success
            ? $"{filterName} başarıyla uygulandı."
            : $"UYARI: {filterName} bulunamadı veya uygulanamadı.");

        return success;
    }

    public async Task WaitForSearchResultsAsync(TimeSpan? timeout = null)
    {
        Report("Arama sonuçlarının yüklenmesi bekleniyor...");

        var isReady = await WaitForScriptTrueOrTimeoutAsync(
            """
            (() => {
                const cards = document.querySelectorAll('#car_card_list .car-card, .car-card, .py-2.car-card');
                const bodyText = (document.body.innerText || '').toLocaleLowerCase('tr-TR');
                return cards.length > 0
                    || bodyText.includes('araç bulundu')
                    || bodyText.includes('hemen kirala')
                    || bodyText.includes('günlük fiyat');
            })();
            """,
            timeout ?? TimeSpan.FromSeconds(30),
            TimeSpan.FromMilliseconds(500));

        Report(isReady
            ? "Arama sonuçları sayfada göründü."
            : "Uyarı: Arama sonuç kartları zaman aşımı süresinde görünmedi.");
    }

    public async Task<List<SearchResultItem>> ReadSearchResultsAsync()
    {
        Report("Sonuç kartları okunuyor...");

        try
        {
            var items = await EvaluateJsonScriptAsync<List<SearchResultItem>>(
                """
                (() => {
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

                    const items = cards.map(card => {
                        const specs = Array.from(card.querySelectorAll('.icon-gear-type, .icon-gas-type'))
                            .map(icon => normalize(icon.parentElement?.textContent))
                            .filter(Boolean);

                        const title = normalize(card.querySelector('.text-dark-gray.text-lg.font-bold, .car-title, h3, h4')?.textContent);
                        const subtitle = normalize(card.querySelector('[data-cms-key="or_similar"], .car-subtitle')?.textContent);
                        const price = normalize(card.querySelector('#car_total_price, .price, .total-price')?.textContent);
                        const dailyPrice = normalize(card.querySelector('[data-cms-key="text_daily_price2"], .daily-price')?.textContent);
                        const transmission = specs.find(text => /manuel|otomatik/i.test(text)) || '';
                        const fuelType = specs.find(text => /benzin|dizel|hibrit|elektrik/i.test(text)) || '';
                        const supplier = normalize(card.querySelector('figure img[alt], .supplier-logo img')?.getAttribute('alt'));
                        const pickupInfo = normalize(card.querySelector('.icon-filled')?.parentElement?.textContent);
                        const actionText = normalize(card.querySelector('[data-cms-key="button_rent_now"], button')?.textContent);
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
                    }).filter(item => item.title || item.price);

                    return JSON.stringify(items);
                })();
                """);

            Report($"{items?.Count ?? 0} sonuç başarıyla okundu.");
            return items ?? new List<SearchResultItem>();
        }
        catch (Exception ex)
        {
            Report($"Sonuç okuma JSON hatası: {ex.Message}");
            return new List<SearchResultItem>();
        }
    }
}

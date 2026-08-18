using System.Text.Json;
using Yolcu360Otomasyon.Models;

namespace Yolcu360Otomasyon.Services;

public sealed partial class BAService
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
                const isVisible = window.__ba?.isVisible || (() => false);

                const filterContainer = document.querySelector('.filter-container');
                if (!isVisible(filterContainer)) return false;

                return Array
                    .from(filterContainer.querySelectorAll('label[name^="filter-transmission."], label[name^="filter-fuel."]'))
                    .some(isVisible);
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

                const normalize = window.__ba?.normalizeTr || (value => (value || '').toLocaleLowerCase('tr-TR').replace(/\s+/g, ' ').trim());
                const isVisible = window.__ba?.isVisible || (() => false);

                const normalizedTargets = targets.map(normalize);

                const labels = Array.from(document.querySelectorAll(`label[name^="${prefix}."]`))
                    .filter(isVisible);

                const matchesTarget = text =>
                    normalizedTargets.some(target =>
                        text === target ||
                        text.startsWith(target + ' ')
                    );

                const match = labels.find(label => matchesTarget(normalize(label.textContent || '')));
                if (!match) return false;

                match.scrollIntoView({ block: 'center', inline: 'nearest' });
                match.click();

                const input = match.querySelector('input[type="checkbox"], input[type="radio"]');
                if (input && !input.checked) {
                    input.click();
                    input.dispatchEvent(new Event('change', { bubbles: true }));
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
                const isVisible = window.__ba?.isVisible || (() => false);

                const hasVisibleCarCard = Array
                    .from(document.querySelectorAll('#car_card_list .car-card'))
                    .some(isVisible);

                if (hasVisibleCarCard) return true;

                const resultArea = document.querySelector('#car_card_list') ||
                    document.querySelector('[id*="car"][id*="list"]') ||
                    document.querySelector('[class*="car-card"]')?.parentElement;

                const loadingText = [
                    'yükleniyor',
                    'aranıyor',
                    'bekleyiniz',
                    'loading'
                ];

                const text = (document.body?.innerText || '').toLocaleLowerCase('tr-TR');
                const hasLoadingText = loadingText.some(item => text.includes(item));

                const hasVisibleLoader = Array
                    .from(document.querySelectorAll('[class*="loading"], [class*="spinner"], [class*="skeleton"], [aria-busy="true"]'))
                    .some(isVisible);

                if (resultArea && isVisible(resultArea) && !hasVisibleLoader && !hasLoadingText) {
                    return true;
                }

                return text.includes('sonuç bulunamadı') ||
                    text.includes('aradığınız kriterlere uygun bir sonuç bulamadık') ||
                    text.includes('araç bulunamadı') ||
                    text.includes('uygun araç bulunamadı') ||
                    text.includes('kriterlerinize uygun') ||
                    text.includes('no result');
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
        await ScrollToLoadAllCardsAsync("#car_card_list .car-card", "Araç sonuçları");

        Report("Sonuç kartları okunuyor...");

        try
        {
            var items = await EvaluateJsonScriptAsync<List<SearchResultItem>>(
                """
                (() => {
                    const normalize = window.__ba?.normalizeText || (value => (value || '').replace(/\s+/g, ' ').trim());
                    const isVisible = window.__ba?.isVisible || (() => false);

                    const firstVisibleText = (root, selector) => {
                        const element = Array.from(root.querySelectorAll(selector)).find(isVisible);
                        return normalize(element?.textContent);
                    };

                    const cards = Array.from(document.querySelectorAll('#car_card_list .car-card'))
                        .filter(isVisible);

                    const items = cards.map(card => {
                        const specs = Array.from(card.querySelectorAll('.icon-gear-type, .icon-gas-type'))
                            .map(icon => normalize(icon.parentElement?.textContent))
                            .filter(Boolean);

                        const title = firstVisibleText(card, '.text-dark-gray.text-lg.font-bold');
                        const subtitle = firstVisibleText(card, '[data-cms-key="or_similar"]');
                        const price = firstVisibleText(card, '#car_total_price');
                        const dailyPrice = firstVisibleText(card, '[data-cms-key="text_daily_price2"]');
                        const transmission = specs.find(text => /manuel|otomatik/i.test(text)) || '';
                        const fuelType = specs.find(text => /benzin|dizel|hibrit|hybrid|elektrik|electric/i.test(text)) || '';
                        const supplier = normalize(card.querySelector('figure img[alt]')?.getAttribute('alt'));
                        const pickupInfo = normalize(card.querySelector('.icon-filled')?.parentElement?.textContent);
                        const actionText = firstVisibleText(card, '[data-cms-key="button_rent_now"]');
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

    private async Task ScrollToLoadAllCardsAsync(string cardSelector, string listDescription)
    {
        Report($"{listDescription} taranıyor ve tüm sonuçlar yükleniyor...");

        var lastCount = -1;
        var unchangedCount = 0;
        const int maxScrollSteps = 25;

        for (var step = 0; step < maxScrollSteps; step++)
        {
            var cardCount = await EvaluateJsonScriptAsync<int>(
                $$"""
                (() => {
                    const selector = {{JsonSerializer.Serialize(cardSelector)}};
                    const isVisible = window.__ba?.isVisible || (() => false);
                    const cards = Array.from(document.querySelectorAll(selector)).filter(isVisible);

                    // 1. "Daha Fazla", "Daha Fazla Göster", "Tümünü Gör" vb. butonlar varsa tıkla
                    const buttons = Array.from(document.querySelectorAll('button, a, div[role="button"], span'));
                    for (const btn of buttons) {
                        const text = (btn.textContent || '').toLocaleLowerCase('tr-TR').trim();
                        if ((text.includes('daha fazla') || text.includes('daha çok') || text.includes('tümünü göster') || text.includes('fazla uçuş') || text.includes('fazla araç')) &&
                            (btn.offsetWidth > 0 || btn.offsetHeight > 0)) {
                            try {
                                btn.click();
                            } catch {}
                        }
                    }

                    // 2. Sayfayı en alta kaydır
                    window.scrollTo({
                        top: Math.max(document.body.scrollHeight, document.documentElement.scrollHeight),
                        behavior: 'smooth'
                    });

                    if (document.documentElement) {
                        document.documentElement.scrollTop = document.documentElement.scrollHeight;
                    }
                    if (document.body) {
                        document.body.scrollTop = document.body.scrollHeight;
                    }

                    // 3. Özel liste kapsayıcısı varsa kaydır
                    const listContainer = document.querySelector('#flight_card_list') || document.querySelector('#car_card_list');
                    if (listContainer) {
                        let parent = listContainer.parentElement;
                        while (parent && parent !== document.body) {
                            if (parent.scrollHeight > parent.clientHeight) {
                                parent.scrollTop = parent.scrollHeight;
                            }
                            parent = parent.parentElement;
                        }
                        try {
                            listContainer.scrollIntoView({ block: 'end', inline: 'nearest' });
                        } catch {}
                    }

                    return cards.length;
                })();
                """);

            if (cardCount > 0)
            {
                Report($"{listDescription}: {cardCount} sonuç yüklendi...");
            }

            if (cardCount == lastCount && cardCount > 0)
            {
                unchangedCount++;
                if (unchangedCount >= 2)
                {
                    break;
                }
            }
            else
            {
                unchangedCount = 0;
                lastCount = cardCount;
            }

            await Task.Delay(850);
        }

        await EvaluateScriptAsync("window.scrollTo({ top: 0, behavior: 'smooth' });");
        await Task.Delay(250);
    }
}

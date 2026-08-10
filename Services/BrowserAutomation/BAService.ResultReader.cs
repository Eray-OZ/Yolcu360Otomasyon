using Yolcu360Otomasyon.Models;

namespace Yolcu360Otomasyon.Services;

public sealed partial class BAService
{
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

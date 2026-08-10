namespace Yolcu360Otomasyon.Services;

public sealed partial class BAService
{
    public async Task WaitForSearchResultsAsync(TimeSpan? timeout = null)
    {
        var deadline = DateTimeOffset.UtcNow + (timeout ?? TimeSpan.FromSeconds(30));
        Report("Arama sonuçlarının yüklenmesi bekleniyor...");

        while (DateTimeOffset.UtcNow < deadline)
        {
            var isReady = await EvaluateBooleanScriptAsync(
                """
                (() => {
                    const cards = document.querySelectorAll('#car_card_list .car-card, .car-card, .py-2.car-card');
                    const bodyText = (document.body.innerText || '').toLocaleLowerCase('tr-TR');
                    return cards.length > 0
                        || bodyText.includes('araç bulundu')
                        || bodyText.includes('hemen kirala')
                        || bodyText.includes('günlük fiyat');
                })();
                """);

            if (isReady)
            {
                Report("Arama sonuçları sayfada göründü.");
                return;
            }

            await Task.Delay(ResultsPollingDelay);
        }

        Report("Uyarı: Arama sonuç kartları zaman aşımı süresinde görünmedi.");
    }
}

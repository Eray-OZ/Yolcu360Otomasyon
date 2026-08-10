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
        await Task.Delay(FilterPanelReadyDelay);

        if (hasTransmission)
        {
            var targetTexts = GetTransmissionFilterTargets(filter.TransmissionType);

            if (targetTexts.Length > 0)
            {
                await ClickFilterOptionAsync("Vites filtresi", "filter-transmission", targetTexts);
                await Task.Delay(FilterRefreshDelay);
            }
        }

        if (hasFuel)
        {
            var targetTexts = GetFuelFilterTargets(filter.FuelType);

            if (targetTexts.Length > 0)
            {
                await ClickFilterOptionAsync("Yakıt filtresi", "filter-fuel", targetTexts);
                await Task.Delay(FilterRefreshDelay);
            }
        }

        Report("Filtreler uygulandı, sonuçların yenilenmesi bekleniyor...");
        await Task.Delay(ResultsRefreshDelay);
        await WaitForSearchResultsAsync();
    }
}

using Yolcu360Otomasyon.Models;

namespace Yolcu360Otomasyon;

public partial class MainWindow
{
    private async Task<List<SearchResultItem>> RunEmbeddedSearchAsync(SearchFilter filter)
    {
        ShowBrowserSection();
        SetSearchStatus("Gömülü tarayıcı arama formu hazırlanıyor...");

        var baService = GetBAService();
        if (_activeUser is not null && !string.IsNullOrWhiteSpace(_activeUser.SessionStatePath))
            await baService.RestoreSessionAsync(_activeUser.SessionStatePath);

        SetSearchStatus("Araçlar aranıyor...");

        await baService.OpenYolcu360HomeAsync();
        await baService.FillPickupLocationAsync(filter.PickupLocation);
        await baService.SelectDateRangeAsync(filter.PickupDate, filter.ReturnDate);
        await baService.SelectTimeAsync(0, filter.PickupTime);
        await baService.SelectTimeAsync(1, filter.ReturnTime);
        await baService.ClickSearchButtonAsync();
        await baService.WaitForSearchResultsAsync();
        await baService.ApplyResultFiltersAsync(filter);

        SetSearchStatus("Arama sonuçları okunuyor...");
        return await baService.ReadSearchResultsAsync();
    }
}

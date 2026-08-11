using Yolcu360Otomasyon.Models;

namespace Yolcu360Otomasyon.Services;

// Extra - Dynamic Collections START
public sealed class DynamicCollectionService
{
    private readonly DatabaseService _databaseService;

    public DynamicCollectionService(DatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public Task<int> SaveSnapshotAsync(
        int kullaniciId,
        string ozelAd,
        SearchFilter filter,
        IReadOnlyCollection<SearchResultItem> currentResults)
    {
        return _databaseService.SaveCollectionAsync(kullaniciId, ozelAd, filter, currentResults);
    }

    public async Task<List<SearchResultItem>> RefreshSnapshotAsync(
        int kullaniciId,
        int koleksiyonId,
        BAService baService,
        string? sessionStatePath = null)
    {
        var filter = await _databaseService.GetCollectionSearchFilterAsync(koleksiyonId, kullaniciId);
        if (filter is null)
            throw new InvalidOperationException("Güncellenecek koleksiyon bulunamadı.");

        if (!string.IsNullOrWhiteSpace(sessionStatePath))
            await baService.RestoreSessionAsync(sessionStatePath);

        await baService.OpenYolcu360HomeAsync();
        await baService.FillPickupLocationAsync(filter.PickupLocation);
        await baService.SelectDateRangeAsync(filter.PickupDate, filter.ReturnDate);
        await baService.SelectTimeAsync(0, filter.PickupTime);
        await baService.SelectTimeAsync(1, filter.ReturnTime);
        await baService.ClickSearchButtonAsync();
        await baService.WaitForSearchResultsAsync();
        await baService.ApplyResultFiltersAsync(filter);

        var refreshedResults = await baService.ReadSearchResultsAsync();
        await _databaseService.ReplaceCollectionVehiclesAsync(koleksiyonId, kullaniciId, refreshedResults);

        return refreshedResults;
    }
}
// Extra - Dynamic Collections END

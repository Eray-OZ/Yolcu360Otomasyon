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

        filter = NormalizeRefreshDateRange(filter);

        if (!string.IsNullOrWhiteSpace(sessionStatePath))
            await baService.RestoreSessionAsync(sessionStatePath);

        await baService.OpenYolcu360HomeAsync();
        await baService.FillPickupLocationAsync(filter.PickupLocation);
        // Extra - Dropoff Location START
        if (!string.IsNullOrWhiteSpace(filter.DropoffLocation))
            await baService.FillDropoffLocationAsync(filter.DropoffLocation);
        // Extra - Dropoff Location END
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

    private static SearchFilter NormalizeRefreshDateRange(SearchFilter filter)
    {
        var today = DateTime.Today;
        var pickupDate = filter.PickupDate.Date;
        var returnDate = filter.ReturnDate.Date;

        if (returnDate < today)
            throw new InvalidOperationException("Bu koleksiyonun tarih aralığı geçmişte kaldığı için güncellenemez.");

        if (pickupDate < today)
            pickupDate = today;

        return new SearchFilter
        {
            PickupLocation = filter.PickupLocation,
            // Extra - Dropoff Location START
            DropoffLocation = filter.DropoffLocation,
            // Extra - Dropoff Location END
            PickupDate = pickupDate,
            ReturnDate = returnDate,
            PickupTime = filter.PickupTime,
            ReturnTime = filter.ReturnTime,
            TransmissionType = filter.TransmissionType,
            FuelType = filter.FuelType
        };
    }
}
// Extra - Dynamic Collections END

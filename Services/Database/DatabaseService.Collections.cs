using Microsoft.EntityFrameworkCore;
using Yolcu360Otomasyon.Data;
using Yolcu360Otomasyon.Models;

namespace Yolcu360Otomasyon.Services;

public sealed partial class DatabaseService
{
    public async Task<int> SaveCollectionAsync(int kullaniciId, string ozelAd, SearchFilter filter, IReadOnlyCollection<SearchResultItem> items)
    {
        await using var context = await CreateContextAsync();
        await EnsureUserExistsAsync(context, kullaniciId);

        var koleksiyon = new Koleksiyon
        {
            KullaniciId = kullaniciId,
            OzelAd = Truncate(ozelAd, 250),
            AlisYeri = Truncate(filter.PickupLocation, 250),
            AlisTarihi = filter.PickupDate,
            DonusTarihi = filter.ReturnDate,
            AlisSaati = Truncate(filter.PickupTime, 16),
            DonusSaati = Truncate(filter.ReturnTime, 16),
            SecilenVitesFiltresi = Truncate(filter.TransmissionType, 64),
            SecilenYakitFiltresi = Truncate(filter.FuelType, 64),
            OlusturmaTarihi = DateTime.UtcNow,
            Araclar = items.Select(ToAracEntity).ToList()
        };

        context.Koleksiyonlar.Add(koleksiyon);
        await context.SaveChangesAsync();
        return koleksiyon.Id;
    }

    public async Task<List<KoleksiyonListItem>> GetCollectionsAsync(int kullaniciId)
    {
        await using var context = await CreateContextAsync();

        return await context.Koleksiyonlar
            .AsNoTracking()
            .Where(item => item.KullaniciId == kullaniciId)
            .OrderByDescending(item => item.OlusturmaTarihi)
            .Select(item => new KoleksiyonListItem
            {
                Id = item.Id,
                OzelAd = item.OzelAd,
                AlisYeri = item.AlisYeri,
                AlisTarihi = item.AlisTarihi,
                DonusTarihi = item.DonusTarihi,
                AlisSaati = item.AlisSaati,
                DonusSaati = item.DonusSaati,
                SecilenVitesFiltresi = string.IsNullOrWhiteSpace(item.SecilenVitesFiltresi) ? "-" : item.SecilenVitesFiltresi,
                SecilenYakitFiltresi = string.IsNullOrWhiteSpace(item.SecilenYakitFiltresi) ? "-" : item.SecilenYakitFiltresi,
                AracSayisi = item.Araclar.Count,
                OlusturmaTarihi = item.OlusturmaTarihi
            })
            .ToListAsync();
    }

    public async Task<List<SearchResultItem>> GetCollectionVehiclesAsync(int koleksiyonId)
    {
        await using var context = await CreateContextAsync();

        var vehicles = await context.Araclar
            .AsNoTracking()
            .Where(item => item.KoleksiyonId == koleksiyonId)
            .OrderBy(item => item.Id)
            .ToListAsync();

        return vehicles.Select(ToSearchResultItem).ToList();
    }

    public async Task DeleteCollectionAsync(int koleksiyonId, int kullaniciId)
    {
        await using var context = await CreateContextAsync();
        var koleksiyon = await context.Koleksiyonlar
            .FirstOrDefaultAsync(item => item.Id == koleksiyonId && item.KullaniciId == kullaniciId);

        if (koleksiyon is null)
            return;

        context.Koleksiyonlar.Remove(koleksiyon);
        await context.SaveChangesAsync();
    }

    private static async Task EnsureUserExistsAsync(AppDbContext context, int kullaniciId)
    {
        var kullaniciVarMi = await context.Kullanicilar
            .AsNoTracking()
            .AnyAsync(item => item.Id == kullaniciId);

        if (!kullaniciVarMi)
            throw new InvalidOperationException("Aktif kullanıcı kaydı veritabanında bulunamadı.");
    }

    private static Arac ToAracEntity(SearchResultItem item)
    {
        return new Arac
        {
            Baslik = Truncate(item.Title, 250),
            AltBaslik = Truncate(item.Subtitle, 250),
            Fiyat = Truncate(item.Price, 64),
            GunlukFiyat = Truncate(item.DailyPrice, 64),
            Vites = Truncate(item.Transmission, 64),
            Yakit = Truncate(item.FuelType, 64),
            Sirket = Truncate(item.Supplier, 128),
            TeslimBilgisi = Truncate(item.PickupInfo, 255),
            IslemMetni = Truncate(item.ActionText, 128),
            Baglanti = Truncate(item.Url, 1024)
        };
    }

    private static SearchResultItem ToSearchResultItem(Arac item)
    {
        return new SearchResultItem
        {
            Title = item.Baslik,
            Subtitle = item.AltBaslik,
            Price = item.Fiyat,
            DailyPrice = item.GunlukFiyat,
            Transmission = item.Vites,
            FuelType = item.Yakit,
            Supplier = item.Sirket,
            PickupInfo = item.TeslimBilgisi,
            ActionText = item.IslemMetni,
            Url = item.Baglanti
        };
    }

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}

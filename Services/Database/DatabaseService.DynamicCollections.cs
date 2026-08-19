using Microsoft.EntityFrameworkCore;
using Yolcu360Otomasyon.Data;
using Yolcu360Otomasyon.Models;

namespace Yolcu360Otomasyon.Services;

// Extra - Dynamic Collections START
public sealed partial class DatabaseService
{
    public async Task<SearchFilter?> GetCollectionSearchFilterAsync(int koleksiyonId, int kullaniciId)
    {
        await EnsureSchemaAsync();
        await using var context = new AppDbContext(_options);

        return await context.Koleksiyonlar
            .AsNoTracking()
            .Where(item => item.Id == koleksiyonId && item.KullaniciId == kullaniciId)
            .Select(item => new SearchFilter
            {
                PickupLocation = item.AlisYeri,
                PickupDate = item.AlisTarihi,
                ReturnDate = item.DonusTarihi,
                PickupTime = item.AlisSaati,
                ReturnTime = item.DonusSaati,
                TransmissionType = item.SecilenVitesFiltresi ?? string.Empty,
                FuelType = item.SecilenYakitFiltresi ?? string.Empty
            })
            .FirstOrDefaultAsync();
    }

    public async Task ReplaceCollectionVehiclesAsync(
        int koleksiyonId,
        int kullaniciId,
        SearchFilter filter,
        IReadOnlyCollection<SearchResultItem> currentResults)
    {
        await EnsureSchemaAsync();
        await using var context = new AppDbContext(_options);

        var koleksiyon = await context.Koleksiyonlar
            .FirstOrDefaultAsync(item => item.Id == koleksiyonId && item.KullaniciId == kullaniciId);

        if (koleksiyon is null)
            throw new InvalidOperationException("Araçları güncellenecek koleksiyon bulunamadı.");

        koleksiyon.AlisTarihi = filter.PickupDate;
        koleksiyon.DonusTarihi = filter.ReturnDate;

        var eskiAraclar = await context.Araclar
            .Where(item => item.KoleksiyonId == koleksiyonId)
            .ToListAsync();

        context.Araclar.RemoveRange(eskiAraclar);

        var yeniAraclar = currentResults.Select(item => new Arac
        {
            KoleksiyonId = koleksiyonId,
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
        });

        await context.Araclar.AddRangeAsync(yeniAraclar);
        await context.SaveChangesAsync();
    }
}
// Extra - Dynamic Collections END

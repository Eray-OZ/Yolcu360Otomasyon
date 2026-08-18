using Microsoft.EntityFrameworkCore;
using System.Globalization;
using Yolcu360Otomasyon.Data;
using Yolcu360Otomasyon.Models;

namespace Yolcu360Otomasyon.Services;

// Extra - Statistics START
public sealed class StatisticsService
{
    private readonly DbContextOptions<AppDbContext> _options;

    public StatisticsService(string connectionString)
    {
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))
            .Options;
    }

    public async Task<IstatistikOzet> GetSummaryAsync(int kullaniciId)
    {
        await using var context = new AppDbContext(_options);

        var collections = await context.Koleksiyonlar
            .AsNoTracking()
            .Include(item => item.Araclar)
            .Where(item => item.KullaniciId == kullaniciId)
            .ToListAsync();

        var payments = (await context.Odemeler
            .AsNoTracking()
            .Where(item => item.KullaniciId == kullaniciId)
            .ToListAsync())
            .Where(item => IsSuccessfulPayment(item.Durum))
            .ToList();

        var allVehicles = collections.SelectMany(item => item.Araclar).ToList();
        var vehiclePrices = allVehicles
            .Select(v => DatabaseService.ParseCurrency(v.Fiyat))
            .Where(p => p > 0)
            .ToList();

        var carPayments = payments.Where(p => p.KoleksiyonId != null || !p.KoleksiyonAdi.StartsWith("[Uçak Bileti]", StringComparison.OrdinalIgnoreCase)).ToList();
        var flightPayments = payments.Where(p => p.KoleksiyonId == null && p.KoleksiyonAdi.StartsWith("[Uçak Bileti]", StringComparison.OrdinalIgnoreCase)).ToList();

        var totalPayment = payments.Sum(item => item.Tutar);
        var totalCount = payments.Count;

        return new IstatistikOzet
        {
            KoleksiyonSayisi = collections.Count,
            AracSayisi = allVehicles.Count,
            OdemeSayisi = totalCount,
            ToplamOdeme = totalPayment,
            AracOdemeSayisi = carPayments.Count,
            AracToplamOdeme = carPayments.Sum(p => p.Tutar),
            UcakOdemeSayisi = flightPayments.Count,
            UcakToplamOdeme = flightPayments.Sum(p => p.Tutar),
            OrtalamaOdeme = totalCount == 0 ? 0m : Math.Round(totalPayment / totalCount, 2),
            EnYuksekKiralama = totalCount == 0 ? 0m : payments.Max(item => item.Tutar),
            EnDusukKiralama = totalCount == 0 ? 0m : payments.Min(item => item.Tutar),
            EnDusukAracFiyati = vehiclePrices.Count == 0 ? 0m : vehiclePrices.Min(),
            EnYuksekAracFiyati = vehiclePrices.Count == 0 ? 0m : vehiclePrices.Max(),
            OrtalamaAracFiyati = vehiclePrices.Count == 0 ? 0m : Math.Round(vehiclePrices.Average(), 2),
            EnCokKiralananAraclar = payments
                .Select(item => ExtractVehicleName(item.KoleksiyonAdi))
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .GroupBy(item => item, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key)
                .Take(5)
                .Select(group => new IstatistikSatir { Ad = group.Key, Sayi = group.Count() })
                .ToList(),
            EnCokKiralananSehirler = collections
                .Select(item => ExtractCityName(item.AlisYeri))
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .GroupBy(item => NormalizeCityKey(item))
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key)
                .Take(5)
                .Select(group => new IstatistikSatir { Ad = group.First(), Sayi = group.Count() })
                .ToList(),
            EnCokTedarikciler = allVehicles
                .Where(v => !string.IsNullOrWhiteSpace(v.Sirket))
                .GroupBy(v => v.Sirket.Trim(), StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key)
                .Take(5)
                .Select(group => new IstatistikSatir { Ad = group.Key, Sayi = group.Count() })
                .ToList(),
            VitesDagitimi = allVehicles
                .Where(v => !string.IsNullOrWhiteSpace(v.Vites))
                .GroupBy(v => v.Vites.Trim(), StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key)
                .Take(5)
                .Select(group => new IstatistikSatir { Ad = group.Key, Sayi = group.Count() })
                .ToList()
        };
    }

    private static bool IsSuccessfulPayment(string? status)
    {
        return string.Equals(status, "success", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status, "SUCCESS", StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractVehicleName(string? collectionName)
    {
        var value = collectionName?.Trim() ?? string.Empty;
        var start = value.LastIndexOf('(');
        var end = value.LastIndexOf(')');

        return start >= 0 && end > start
            ? value[(start + 1)..end].Trim()
            : string.Empty;
    }

    private static string ExtractCityName(string? location)
    {
        var value = location?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var commaIndex = value.IndexOf(',');
        if (commaIndex > 0)
            value = value[..commaIndex];

        return value
            .Replace("Havalimanı", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("Airport", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();
    }

    private static string NormalizeCityKey(string value)
    {
        return value
            .ToLower(new CultureInfo("tr-TR"))
            .Replace('ı', 'i');
    }
}
// Extra - Statistics END

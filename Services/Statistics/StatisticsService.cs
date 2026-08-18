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

        return new IstatistikOzet
        {
            KoleksiyonSayisi = collections.Count,
            AracSayisi = collections.Sum(item => item.Araclar.Count),
            OdemeSayisi = payments.Count,
            ToplamOdeme = payments.Sum(item => item.Tutar),
            EnYuksekKiralama = payments.Count == 0 ? 0m : payments.Max(item => item.Tutar),
            EnDusukKiralama = payments.Count == 0 ? 0m : payments.Min(item => item.Tutar),
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

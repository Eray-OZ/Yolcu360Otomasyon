using Microsoft.EntityFrameworkCore;
using Yolcu360Otomasyon.Data;
using Yolcu360Otomasyon.Models;

namespace Yolcu360Otomasyon.Services;

public sealed partial class DatabaseService
{
    public async Task<List<OdemeHazirlikItem>> GetPaymentPreviewAsync(int kullaniciId, IReadOnlyCollection<int> koleksiyonIds)
    {
        await EnsureSchemaAsync();
        await using var context = new AppDbContext(_options);

        var collections = await context.Koleksiyonlar
            .Include(item => item.Araclar)
            .Where(item => item.KullaniciId == kullaniciId && koleksiyonIds.Contains(item.Id))
            .ToListAsync();

        return collections.Select(collection => new OdemeHazirlikItem
        {
            KoleksiyonId = collection.Id,
            KoleksiyonAdi = collection.OzelAd,
            Tutar = CalculatePaymentAmount(collection.Araclar)
        }).ToList();
    }

    public async Task CreatePaymentsFromSandboxResultAsync(
        int kullaniciId,
        IReadOnlyList<OdemeHazirlikItem> previewItems,
        IyzicoPaymentResult paymentResult)
    {
        await EnsureSchemaAsync();
        await using var context = new AppDbContext(_options);

        foreach (var item in previewItems)
        {
            context.Odemeler.Add(new Odeme
            {
                KullaniciId = kullaniciId,
                KoleksiyonId = item.KoleksiyonId.HasValue && item.KoleksiyonId.Value > 0 ? item.KoleksiyonId.Value : null,
                ReferansNo = paymentResult.ReferenceNo,
                KoleksiyonAdi = item.KoleksiyonAdi,
                Tutar = item.Tutar,
                ParaBirimi = "TRY",
                Durum = string.IsNullOrWhiteSpace(paymentResult.PaymentStatus) ? paymentResult.Status : paymentResult.PaymentStatus,
                Saglayici = paymentResult.Provider,
                KartSahibi = paymentResult.CardHolderName,
                KartSon4 = paymentResult.LastFourDigits,
                OdemeTarihi = DateTime.UtcNow
            });
        }

        await context.SaveChangesAsync();
    }

    public async Task<List<OdemeListItem>> GetPaymentsAsync(int kullaniciId)
    {
        await EnsureSchemaAsync();
        await using var context = new AppDbContext(_options);

        return await context.Odemeler
            .AsNoTracking()
            .Where(item => item.KullaniciId == kullaniciId)
            .OrderByDescending(item => item.OdemeTarihi)
            .Select(item => new OdemeListItem
            {
                Id = item.Id,
                ReferansNo = item.ReferansNo,
                KoleksiyonAdi = item.KoleksiyonAdi,
                Tutar = item.Tutar,
                ParaBirimi = item.ParaBirimi,
                Durum = item.Durum,
                Saglayici = item.Saglayici,
                KartSahibi = item.KartSahibi,
                KartSon4 = item.KartSon4,
                OdemeTarihi = item.OdemeTarihi
            })
            .ToListAsync();
    }

    private static decimal CalculatePaymentAmount(IEnumerable<Arac> araclar)
    {
        var parsedPrices = araclar
            .Select(item => ParseCurrency(item.Fiyat))
            .Where(item => item > 0)
            .ToList();

        if (parsedPrices.Count == 0)
            return 0m;

        return parsedPrices.Min();
    }

    public static decimal ParseCurrency(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return 0m;

        var raw = value.Trim()
            .Replace("TL", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("TRY", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();

        var digits = new string(raw.Where(ch => char.IsDigit(ch) || ch == ',' || ch == '.').ToArray());
        if (string.IsNullOrWhiteSpace(digits))
            return 0m;

        if (digits.Contains('.') && digits.Contains(','))
        {
            digits = digits.Replace(".", "").Replace(',', '.');
        }
        else if (digits.Contains('.'))
        {
            var parts = digits.Split('.');
            if (parts.Length > 1 && parts[^1].Length == 3)
            {
                digits = digits.Replace(".", "");
            }
            else
            {
                digits = digits.Replace('.', ',');
            }
        }
        else if (digits.Contains(','))
        {
            var parts = digits.Split(',');
            if (parts.Length > 1 && parts[^1].Length == 3)
            {
                digits = digits.Replace(",", "");
            }
            else
            {
                digits = digits.Replace(',', '.');
            }
        }

        return decimal.TryParse(digits, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var result)
            ? result
            : 0m;
    }
}

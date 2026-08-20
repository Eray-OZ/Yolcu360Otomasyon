// Extra - Car Comparison START
using Yolcu360Otomasyon.Models;

namespace Yolcu360Otomasyon.Services;

public sealed class CarComparisonService
{
    public List<CarComparisonItem> BuildComparison(IReadOnlyList<SearchResultItem> vehicles, string? sourceName = null)
    {
        if (vehicles.Count == 0)
            return new List<CarComparisonItem>();

        var items = new List<CarComparisonItem>();

        foreach (var v in vehicles)
        {
            var totalPrice = DatabaseService.ParseCurrency(v.Price);
            var dailyPrice = DatabaseService.ParseCurrency(v.DailyPrice);

            items.Add(new CarComparisonItem
            {
                Vehicle = v,
                TotalPriceNumeric = totalPrice,
                DailyPriceNumeric = dailyPrice,
                SourceCollectionName = sourceName ?? "Arama Sonucu"
            });
        }

        var minPrice = items.Min(x => x.TotalPriceNumeric);
        if (minPrice <= 0) minPrice = 1;

        foreach (var item in items)
        {
            var isCheapest = Math.Abs(item.TotalPriceNumeric - minPrice) < 0.01m;
            item.IsCheapest = isCheapest;

            if (isCheapest)
            {
                item.DifferenceFromCheapest = 0;
                item.PercentageDifference = 0;
                item.PriceBadgeText = "🏆 EN UYGUN FİYAT";
                item.PriceBadgeColor = "#16A34A"; // Yeşil
            }
            else
            {
                var diff = item.TotalPriceNumeric - minPrice;
                var pct = (double)(diff / minPrice) * 100.0;
                item.DifferenceFromCheapest = diff;
                item.PercentageDifference = Math.Round(pct, 1);
                item.PriceBadgeText = $"+{diff:N0} TL (%{item.PercentageDifference:N0} Fark)";
                item.PriceBadgeColor = "#64748B"; // Nötr Gri
            }

            // Avantaj Rozetleri Üretimi
            var badges = new List<string>();

            if (item.IsCheapest)
                badges.Add("💰 En Ekonomik Toplam Tutar");

            var trans = (item.Vehicle.Transmission ?? string.Empty).ToLowerInvariant();
            if (trans.Contains("otomatik") || trans.Contains("automatic"))
                badges.Add("⚡ Otomatik Vites Konforu");
            else if (trans.Contains("manuel"))
                badges.Add("⚙️ Manuel Sürüş Kontrolü");

            var fuel = (item.Vehicle.FuelType ?? string.Empty).ToLowerInvariant();
            if (fuel.Contains("dizel") || fuel.Contains("diesel"))
                badges.Add("⛽ Dizel Yakıt Tasarrufu");
            else if (fuel.Contains("hybrid") || fuel.Contains("hibrit") || fuel.Contains("elektrik"))
                badges.Add("🌱 Çevre Dostu / Düşük Tüketim");

            var pickup = (item.Vehicle.PickupInfo ?? string.Empty).ToLowerInvariant();
            if (pickup.Contains("havaliman") || pickup.Contains("ofis") || pickup.Contains("terminal"))
                badges.Add("🏢 Havalimanı Ofis Teslimi");
            else if (pickup.Contains("karşılama") || pickup.Contains("karsilama") || pickup.Contains("meet"))
                badges.Add("📍 Özel Karşılama Hizmeti");

            if (!string.IsNullOrWhiteSpace(item.Vehicle.Supplier))
                badges.Add($"⭐ {item.Vehicle.Supplier}");

            item.AdvantageBadges = badges;
        }

        return items;
    }
}
// Extra - Car Comparison END

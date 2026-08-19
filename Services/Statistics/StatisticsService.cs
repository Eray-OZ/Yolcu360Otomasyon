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
            EnCokKiralananAraclar = carPayments
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

    private static readonly Dictionary<string, string> KnownModelToBrand = new(StringComparer.OrdinalIgnoreCase)
    {
        // Renault
        ["clio"] = "Renault Clio",
        ["clio 5"] = "Renault Clio",
        ["clio 4"] = "Renault Clio",
        ["megane"] = "Renault Megane",
        ["megane sedan"] = "Renault Megane",
        ["austral"] = "Renault Austral",
        ["captur"] = "Renault Captur",
        ["taliant"] = "Renault Taliant",
        ["kadjar"] = "Renault Kadjar",
        ["koleos"] = "Renault Koleos",
        ["fluence"] = "Renault Fluence",
        ["symbol"] = "Renault Symbol",

        // Fiat
        ["egea"] = "Fiat Egea",
        ["egea sedan"] = "Fiat Egea",
        ["egea cross"] = "Fiat Egea",
        ["egea hatchback"] = "Fiat Egea",
        ["egea hb"] = "Fiat Egea",
        ["500"] = "Fiat 500",
        ["500l"] = "Fiat 500L",
        ["500x"] = "Fiat 500X",
        ["panda"] = "Fiat Panda",
        ["doblo"] = "Fiat Doblo",
        ["fiorino"] = "Fiat Fiorino",
        ["linea"] = "Fiat Linea",

        // Dacia
        ["duster"] = "Dacia Duster",
        ["sandero"] = "Dacia Sandero",
        ["sandero stepway"] = "Dacia Sandero",
        ["jogger"] = "Dacia Jogger",
        ["lodgy"] = "Dacia Lodgy",
        ["dokker"] = "Dacia Dokker",

        // Hyundai
        ["i20"] = "Hyundai i20",
        ["i10"] = "Hyundai i10",
        ["i30"] = "Hyundai i30",
        ["elantra"] = "Hyundai Elantra",
        ["tucson"] = "Hyundai Tucson",
        ["bayon"] = "Hyundai Bayon",
        ["kona"] = "Hyundai Kona",
        ["accent"] = "Hyundai Accent",
        ["accent blue"] = "Hyundai Accent",

        // Peugeot
        ["208"] = "Peugeot 208",
        ["2008"] = "Peugeot 2008",
        ["308"] = "Peugeot 308",
        ["3008"] = "Peugeot 3008",
        ["408"] = "Peugeot 408",
        ["508"] = "Peugeot 508",
        ["5008"] = "Peugeot 5008",
        ["301"] = "Peugeot 301",
        ["rifter"] = "Peugeot Rifter",

        // Volkswagen
        ["polo"] = "Volkswagen Polo",
        ["golf"] = "Volkswagen Golf",
        ["passat"] = "Volkswagen Passat",
        ["t-roc"] = "Volkswagen T-Roc",
        ["troc"] = "Volkswagen T-Roc",
        ["taigo"] = "Volkswagen Taigo",
        ["tiguan"] = "Volkswagen Tiguan",
        ["caddy"] = "Volkswagen Caddy",
        ["t-cross"] = "Volkswagen T-Cross",
        ["tcross"] = "Volkswagen T-Cross",
        ["arteon"] = "Volkswagen Arteon",
        ["jetta"] = "Volkswagen Jetta",

        // Toyota
        ["corolla"] = "Toyota Corolla",
        ["corolla cross"] = "Toyota Corolla Cross",
        ["yaris"] = "Toyota Yaris",
        ["yaris cross"] = "Toyota Yaris Cross",
        ["c-hr"] = "Toyota C-HR",
        ["chr"] = "Toyota C-HR",
        ["rav4"] = "Toyota RAV4",
        ["auris"] = "Toyota Auris",

        // Citroen
        ["c3"] = "Citroen C3",
        ["c3 aircross"] = "Citroen C3",
        ["c4"] = "Citroen C4",
        ["c4 x"] = "Citroen C4",
        ["c5 aircross"] = "Citroen C5 Aircross",
        ["c-elysee"] = "Citroen C-Elysee",
        ["c elysee"] = "Citroen C-Elysee",
        ["berlingo"] = "Citroen Berlingo",

        // Ford
        ["focus"] = "Ford Focus",
        ["fiesta"] = "Ford Fiesta",
        ["puma"] = "Ford Puma",
        ["kuga"] = "Ford Kuga",
        ["courier"] = "Ford Courier",
        ["tourneo courier"] = "Ford Courier",
        ["transit"] = "Ford Transit",
        ["mondeo"] = "Ford Mondeo",

        // Opel
        ["corsa"] = "Opel Corsa",
        ["astra"] = "Opel Astra",
        ["mokka"] = "Opel Mokka",
        ["crossland"] = "Opel Crossland",
        ["grandland"] = "Opel Grandland",
        ["insignia"] = "Opel Insignia",
        ["combo"] = "Opel Combo",

        // Skoda
        ["octavia"] = "Skoda Octavia",
        ["fabia"] = "Skoda Fabia",
        ["scala"] = "Skoda Scala",
        ["kamiq"] = "Skoda Kamiq",
        ["karoq"] = "Skoda Karoq",
        ["kodiaq"] = "Skoda Kodiaq",
        ["superb"] = "Skoda Superb",

        // Seat & Cupra
        ["leon"] = "Seat Leon",
        ["ibiza"] = "Seat Ibiza",
        ["arona"] = "Seat Arona",
        ["ateca"] = "Seat Ateca",
        ["formentor"] = "Cupra Formentor",

        // Kia
        ["stonic"] = "Kia Stonic",
        ["rio"] = "Kia Rio",
        ["cerato"] = "Kia Cerato",
        ["ceed"] = "Kia Ceed",
        ["sportage"] = "Kia Sportage",
        ["picanto"] = "Kia Picanto",
        ["xceed"] = "Kia XCeed",

        // Nissan
        ["qashqai"] = "Nissan Qashqai",
        ["juke"] = "Nissan Juke",
        ["x-trail"] = "Nissan X-Trail",
        ["micra"] = "Nissan Micra",

        // Chery
        ["omoda 5"] = "Chery Omoda 5",
        ["omoda"] = "Chery Omoda 5",
        ["tiggo 7"] = "Chery Tiggo 7",
        ["tiggo 7 pro"] = "Chery Tiggo 7",
        ["tiggo 8"] = "Chery Tiggo 8",
        ["tiggo 8 pro"] = "Chery Tiggo 8"
    };

    private static readonly string[] KnownBrands = new[]
    {
        "Renault", "Fiat", "Dacia", "Hyundai", "Peugeot", "Toyota", "Volkswagen", "VW",
        "Citroen", "Citroën", "Ford", "Nissan", "Skoda", "Škoda", "Opel", "Seat", "Kia",
        "Chery", "Honda", "BMW", "Mercedes", "Mercedes-Benz", "Audi", "Volvo", "Jeep",
        "Suzuki", "Cupra", "MG", "BYD", "SsangYong", "KGM", "Alfa Romeo", "Mitsubishi",
        "Mini", "Tesla", "Mazda"
    };

    private static string ExtractVehicleName(string? collectionName)
    {
        var value = collectionName?.Trim() ?? string.Empty;
        if (value.StartsWith("[Uçak Bileti]", StringComparison.OrdinalIgnoreCase))
            return string.Empty;

        var start = value.LastIndexOf('(');
        var end = value.LastIndexOf(')');

        if (start >= 0 && end > start)
        {
            var extracted = value[(start + 1)..end].Trim();
            if (!extracted.Contains(" - ") && !extracted.StartsWith("ADB") && !extracted.StartsWith("SAW") && !extracted.StartsWith("IST"))
            {
                return NormalizeBrandAndModel(extracted);
            }
        }

        return string.Empty;
    }

    private static string NormalizeBrandAndModel(string rawTitle)
    {
        if (string.IsNullOrWhiteSpace(rawTitle))
            return string.Empty;

        var clean = rawTitle.Trim();

        // 1. Önce doğrudan model haritasından ara (Örn: "Clio 5", "Duster", "Egea Cross" vb.)
        foreach (var (key, canonical) in KnownModelToBrand.OrderByDescending(k => k.Key.Length))
        {
            if (clean.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return canonical;
            }
        }

        // 2. Bilinen markayla başlıyorsa Marka + İlk Model kelimesini birleştir
        foreach (var brand in KnownBrands.OrderByDescending(b => b.Length))
        {
            if (clean.StartsWith(brand, StringComparison.OrdinalIgnoreCase))
            {
                var remaining = clean[brand.Length..].Trim();
                var parts = remaining.Split(new[] { ' ', '-', '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0)
                {
                    var modelName = parts[0];
                    // "5", "4" gibi sadece jenerasyon rakamı ise
                    if (modelName.Length == 1 && char.IsDigit(modelName[0]) && parts.Length > 1)
                    {
                        modelName = parts[1];
                    }
                    return $"{brand} {Capitalize(modelName)}";
                }
                return brand;
            }
        }

        // 3. Marka tespit edilemediyse ilk 2 kelimeyi al ve temizle
        var words = clean.Split(new[] { ' ', '-' }, StringSplitOptions.RemoveEmptyEntries);
        return words.Length switch
        {
            0 => string.Empty,
            1 => Capitalize(words[0]),
            _ => $"{Capitalize(words[0])} {Capitalize(words[1])}"
        };
    }

    private static string Capitalize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        return char.ToUpper(text[0], new CultureInfo("tr-TR")) + text[1..].ToLower(new CultureInfo("tr-TR"));
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

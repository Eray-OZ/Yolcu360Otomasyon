using Microsoft.EntityFrameworkCore;
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

    public async Task EnsureTableAsync()
    {
        await using var context = new AppDbContext(_options);
        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS arama_istatistikleri (
                Id INT NOT NULL AUTO_INCREMENT,
                KullaniciId INT NOT NULL,
                AramaTuru VARCHAR(32) NOT NULL,
                Basarili TINYINT(1) NOT NULL,
                SonucSayisi INT NOT NULL,
                SureMs BIGINT NOT NULL,
                OlusturmaTarihi DATETIME(6) NOT NULL,
                PRIMARY KEY (Id),
                INDEX IX_arama_istatistikleri_KullaniciId (KullaniciId)
            ) CHARACTER SET utf8mb4;
            """);
    }

    public async Task RecordSearchAsync(
        int kullaniciId,
        string aramaTuru,
        bool basarili,
        int sonucSayisi,
        TimeSpan sure)
    {
        await EnsureTableAsync();

        await using var context = new AppDbContext(_options);
        context.AramaIstatistikleri.Add(new AramaIstatistigi
        {
            KullaniciId = kullaniciId,
            AramaTuru = aramaTuru,
            Basarili = basarili,
            SonucSayisi = sonucSayisi,
            SureMs = (long)sure.TotalMilliseconds,
            OlusturmaTarihi = DateTime.UtcNow
        });

        await context.SaveChangesAsync();
    }

    public async Task<IstatistikOzet> GetSummaryAsync(int kullaniciId)
    {
        await EnsureTableAsync();

        await using var context = new AppDbContext(_options);
        var searches = await context.AramaIstatistikleri
            .Where(item => item.KullaniciId == kullaniciId)
            .ToListAsync();

        var collectionIds = await context.Koleksiyonlar
            .Where(item => item.KullaniciId == kullaniciId)
            .Select(item => item.Id)
            .ToListAsync();

        var vehicleCount = collectionIds.Count == 0
            ? 0
            : await context.Araclar.CountAsync(item => collectionIds.Contains(item.KoleksiyonId));

        var payments = await context.Odemeler
            .Where(item => item.KullaniciId == kullaniciId)
            .ToListAsync();

        return new IstatistikOzet
        {
            ToplamArama = searches.Count,
            BasariliArama = searches.Count(item => item.Basarili),
            BasarisizArama = searches.Count(item => !item.Basarili),
            ToplamSonuc = searches.Sum(item => item.SonucSayisi),
            OrtalamaSureSaniye = searches.Count == 0
                ? 0
                : searches.Average(item => item.SureMs) / 1000d,
            KoleksiyonSayisi = collectionIds.Count,
            AracSayisi = vehicleCount,
            OdemeSayisi = payments.Count,
            ToplamOdeme = payments
                .Where(item => item.Durum.Equals("success", StringComparison.OrdinalIgnoreCase))
                .Sum(item => item.Tutar)
        };
    }
}
// Extra - Statistics END

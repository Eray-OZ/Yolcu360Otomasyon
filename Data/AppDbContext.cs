using Microsoft.EntityFrameworkCore;
using Yolcu360Otomasyon.Models;

namespace Yolcu360Otomasyon.Data;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<AppUser> Kullanicilar => Set<AppUser>();
    public DbSet<Koleksiyon> Koleksiyonlar => Set<Koleksiyon>();
    public DbSet<Arac> Araclar => Set<Arac>();
    public DbSet<Odeme> Odemeler => Set<Odeme>();
    // Extra - Statistics START
    public DbSet<AramaIstatistigi> AramaIstatistikleri => Set<AramaIstatistigi>();
    // Extra - Statistics END

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var user = modelBuilder.Entity<AppUser>();
        user.ToTable("kullanicilar");
        user.HasKey(item => item.Id);
        user.HasIndex(item => item.Email).IsUnique();
        user.Property(item => item.Email).HasMaxLength(255).IsRequired();
        user.Property(item => item.Password).HasMaxLength(255).IsRequired();
        user.Property(item => item.PhoneNumber).HasMaxLength(32).IsRequired();
        user.Property(item => item.SessionStatePath).HasMaxLength(512).IsRequired();
        user.Property(item => item.CreatedAt).IsRequired();
        user.Property(item => item.UpdatedAt).IsRequired();

        var koleksiyon = modelBuilder.Entity<Koleksiyon>();
        koleksiyon.ToTable("koleksiyonlar");
        koleksiyon.HasKey(item => item.Id);
        koleksiyon.Property(item => item.OzelAd).HasMaxLength(255).IsRequired();
        koleksiyon.Property(item => item.AlisYeri).HasMaxLength(255).IsRequired();
        koleksiyon.Property(item => item.AlisTarihi).IsRequired();
        koleksiyon.Property(item => item.DonusTarihi).IsRequired();
        koleksiyon.Property(item => item.AlisSaati).HasMaxLength(16).IsRequired();
        koleksiyon.Property(item => item.DonusSaati).HasMaxLength(16).IsRequired();
        koleksiyon.Property(item => item.SecilenVitesFiltresi).HasMaxLength(64).IsRequired(false);
        koleksiyon.Property(item => item.SecilenYakitFiltresi).HasMaxLength(64).IsRequired(false);
        koleksiyon.Property(item => item.OlusturmaTarihi).IsRequired();
        koleksiyon
            .HasOne(item => item.Kullanici)
            .WithMany(item => item.Koleksiyonlar)
            .HasForeignKey(item => item.KullaniciId)
            .OnDelete(DeleteBehavior.Cascade);

        var arac = modelBuilder.Entity<Arac>();
        arac.ToTable("araclar");
        arac.HasKey(item => item.Id);
        arac.Property(item => item.Baslik).HasMaxLength(255).IsRequired();
        arac.Property(item => item.AltBaslik).HasMaxLength(255);
        arac.Property(item => item.Fiyat).HasMaxLength(64).IsRequired();
        arac.Property(item => item.GunlukFiyat).HasMaxLength(64);
        arac.Property(item => item.Vites).HasMaxLength(64);
        arac.Property(item => item.Yakit).HasMaxLength(64);
        arac.Property(item => item.Sirket).HasMaxLength(128);
        arac.Property(item => item.TeslimBilgisi).HasMaxLength(255);
        arac.Property(item => item.IslemMetni).HasMaxLength(128);
        arac.Property(item => item.Baglanti).HasMaxLength(1024);
        arac
            .HasOne(item => item.Koleksiyon)
            .WithMany(item => item.Araclar)
            .HasForeignKey(item => item.KoleksiyonId)
            .OnDelete(DeleteBehavior.Cascade);

        var odeme = modelBuilder.Entity<Odeme>();
        odeme.ToTable("odemeler");
        odeme.HasKey(item => item.Id);
        odeme.Property(item => item.ReferansNo).HasMaxLength(64).IsRequired();
        odeme.Property(item => item.KoleksiyonAdi).HasMaxLength(255).IsRequired();
        odeme.Property(item => item.Tutar).HasPrecision(18, 2).IsRequired();
        odeme.Property(item => item.ParaBirimi).HasMaxLength(8).IsRequired();
        odeme.Property(item => item.Durum).HasMaxLength(32).IsRequired();
        odeme.Property(item => item.Saglayici).HasMaxLength(64).IsRequired();
        odeme.Property(item => item.KartSahibi).HasMaxLength(128).IsRequired(false);
        odeme.Property(item => item.KartSon4).HasMaxLength(4).IsRequired(false);
        odeme.Property(item => item.OdemeTarihi).IsRequired();
        odeme
            .HasOne(item => item.Kullanici)
            .WithMany(item => item.Odemeler)
            .HasForeignKey(item => item.KullaniciId)
            .OnDelete(DeleteBehavior.Cascade);
        odeme
            .HasOne(item => item.Koleksiyon)
            .WithMany(item => item.Odemeler)
            .HasForeignKey(item => item.KoleksiyonId)
            .OnDelete(DeleteBehavior.Cascade);

        // Extra - Statistics START
        var statistic = modelBuilder.Entity<AramaIstatistigi>();
        statistic.ToTable("arama_istatistikleri");
        statistic.HasKey(item => item.Id);
        statistic.Property(item => item.AramaTuru).HasMaxLength(32).IsRequired();
        statistic.Property(item => item.Basarili).IsRequired();
        statistic.Property(item => item.SonucSayisi).IsRequired();
        statistic.Property(item => item.SureMs).IsRequired();
        statistic.Property(item => item.OlusturmaTarihi).IsRequired();
        statistic.HasIndex(item => item.KullaniciId);
        // Extra - Statistics END
    }
}

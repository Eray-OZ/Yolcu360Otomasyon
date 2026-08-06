namespace Yolcu360Otomasyon.Models;

public sealed class Koleksiyon
{
    public int Id { get; set; }
    public int KullaniciId { get; set; }
    public AppUser? Kullanici { get; set; }
    public string OzelAd { get; set; } = string.Empty;
    public string AlisYeri { get; set; } = string.Empty;
    public DateTime AlisTarihi { get; set; }
    public DateTime DonusTarihi { get; set; }
    public string AlisSaati { get; set; } = string.Empty;
    public string DonusSaati { get; set; } = string.Empty;
    public string? SecilenVitesFiltresi { get; set; }
    public string? SecilenYakitFiltresi { get; set; }
    public DateTime OlusturmaTarihi { get; set; }
    public List<Arac> Araclar { get; set; } = new();
    public List<Odeme> Odemeler { get; set; } = new();
}

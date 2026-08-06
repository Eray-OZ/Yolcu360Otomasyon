namespace Yolcu360Otomasyon.Models;

public sealed class Koleksiyon
{
    public int Id { get; set; }
    public int KullaniciId { get; set; }
    public AppUser? Kullanici { get; set; }
    public string OzelAd { get; set; } = string.Empty;
    public DateTime OlusturmaTarihi { get; set; }
    public List<Arac> Araclar { get; set; } = new();
}

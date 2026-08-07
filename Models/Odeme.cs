namespace Yolcu360Otomasyon.Models;

public sealed class Odeme
{
    public int Id { get; set; }
    public int KullaniciId { get; set; }
    public AppUser? Kullanici { get; set; }
    public int KoleksiyonId { get; set; }
    public Koleksiyon? Koleksiyon { get; set; }
    public string ReferansNo { get; set; } = string.Empty;
    public string KoleksiyonAdi { get; set; } = string.Empty;
    public decimal Tutar { get; set; }
    public string ParaBirimi { get; set; } = "TRY";
    public string Durum { get; set; } = "SUCCESS";
    public string Saglayici { get; set; } = "iyzico-sandbox";
    public string? KartSahibi { get; set; }
    public string? KartSon4 { get; set; }
    public DateTime OdemeTarihi { get; set; }
}

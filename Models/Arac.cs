namespace Yolcu360Otomasyon.Models;

public sealed class Arac
{
    public int Id { get; set; }
    public int KoleksiyonId { get; set; }
    public Koleksiyon? Koleksiyon { get; set; }
    public string Baslik { get; set; } = string.Empty;
    public string AltBaslik { get; set; } = string.Empty;
    public string Fiyat { get; set; } = string.Empty;
    public string GunlukFiyat { get; set; } = string.Empty;
    public string Vites { get; set; } = string.Empty;
    public string Yakit { get; set; } = string.Empty;
    public string Sirket { get; set; } = string.Empty;
    public string TeslimBilgisi { get; set; } = string.Empty;
    public string IslemMetni { get; set; } = string.Empty;
    public string Baglanti { get; set; } = string.Empty;
}

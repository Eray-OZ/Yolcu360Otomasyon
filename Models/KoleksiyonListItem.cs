namespace Yolcu360Otomasyon.Models;

public sealed class KoleksiyonListItem
{
    public int Id { get; set; }
    public string OzelAd { get; set; } = string.Empty;
    public string AlisYeri { get; set; } = string.Empty;
    public DateTime AlisTarihi { get; set; }
    public DateTime DonusTarihi { get; set; }
    public string AlisSaati { get; set; } = string.Empty;
    public string DonusSaati { get; set; } = string.Empty;
    public string? SecilenVitesFiltresi { get; set; }
    public string? SecilenYakitFiltresi { get; set; }
    public int AracSayisi { get; set; }
    public DateTime OlusturmaTarihi { get; set; }
}

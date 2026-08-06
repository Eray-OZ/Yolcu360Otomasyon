namespace Yolcu360Otomasyon.Models;

public sealed class OdemeListItem
{
    public int Id { get; set; }
    public string ReferansNo { get; set; } = string.Empty;
    public string KoleksiyonAdi { get; set; } = string.Empty;
    public decimal Tutar { get; set; }
    public string ParaBirimi { get; set; } = "TRY";
    public string Durum { get; set; } = string.Empty;
    public string Saglayici { get; set; } = string.Empty;
    public string? KartSahibi { get; set; }
    public string? KartSon4 { get; set; }
    public DateTime OdemeTarihi { get; set; }
}

namespace Yolcu360Otomasyon.Models;

public sealed class OdemeHazirlikItem
{
    public int? KoleksiyonId { get; set; }
    public string KoleksiyonAdi { get; set; } = string.Empty;
    public decimal Tutar { get; set; }
}

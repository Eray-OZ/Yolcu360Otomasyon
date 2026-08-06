namespace Yolcu360Otomasyon.Models;

public sealed class KoleksiyonListItem
{
    public int Id { get; set; }
    public string OzelAd { get; set; } = string.Empty;
    public int AracSayisi { get; set; }
    public DateTime OlusturmaTarihi { get; set; }
}

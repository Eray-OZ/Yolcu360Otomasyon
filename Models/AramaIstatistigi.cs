namespace Yolcu360Otomasyon.Models;

// Extra - Statistics START
public sealed class AramaIstatistigi
{
    public int Id { get; set; }
    public int KullaniciId { get; set; }
    public string AramaTuru { get; set; } = string.Empty;
    public bool Basarili { get; set; }
    public int SonucSayisi { get; set; }
    public long SureMs { get; set; }
    public DateTime OlusturmaTarihi { get; set; }
}
// Extra - Statistics END

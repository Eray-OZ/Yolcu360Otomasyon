namespace Yolcu360Otomasyon.Models;

// Extra - Statistics START
public sealed class IstatistikSatir
{
    public string Ad { get; init; } = string.Empty;
    public int Sayi { get; init; }
    public string? EkBilgi { get; init; }
}

public sealed class IstatistikOzet
{
    public int KoleksiyonSayisi { get; init; }
    public int AracSayisi { get; init; }
    public int OdemeSayisi { get; init; }
    public decimal ToplamOdeme { get; init; }
    public int AracOdemeSayisi { get; init; }
    public decimal AracToplamOdeme { get; init; }
    public int UcakOdemeSayisi { get; init; }
    public decimal UcakToplamOdeme { get; init; }
    public decimal OrtalamaOdeme { get; init; }
    public decimal EnYuksekKiralama { get; init; }
    public decimal EnDusukKiralama { get; init; }
    public decimal EnDusukAracFiyati { get; init; }
    public decimal EnYuksekAracFiyati { get; init; }
    public decimal OrtalamaAracFiyati { get; init; }
    public IReadOnlyList<IstatistikSatir> EnCokKiralananAraclar { get; init; } = [];
    public IReadOnlyList<IstatistikSatir> EnCokKiralananSehirler { get; init; } = [];
    public IReadOnlyList<IstatistikSatir> EnCokTedarikciler { get; init; } = [];
    public IReadOnlyList<IstatistikSatir> VitesDagitimi { get; init; } = [];
}
// Extra - Statistics END

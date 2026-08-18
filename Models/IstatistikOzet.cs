namespace Yolcu360Otomasyon.Models;

// Extra - Statistics START
public sealed class IstatistikSatir
{
    public string Ad { get; init; } = string.Empty;
    public int Sayi { get; init; }
}

public sealed class IstatistikOzet
{
    public int KoleksiyonSayisi { get; init; }
    public int AracSayisi { get; init; }
    public int OdemeSayisi { get; init; }
    public decimal ToplamOdeme { get; init; }
    public decimal EnYuksekKiralama { get; init; }
    public decimal EnDusukKiralama { get; init; }
    public IReadOnlyList<IstatistikSatir> EnCokKiralananAraclar { get; init; } = [];
    public IReadOnlyList<IstatistikSatir> EnCokKiralananSehirler { get; init; } = [];
}
// Extra - Statistics END

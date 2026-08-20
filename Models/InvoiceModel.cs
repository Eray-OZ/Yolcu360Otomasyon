// Extra - Invoice PDF START
namespace Yolcu360Otomasyon.Models;

public sealed class InvoiceModel
{
    public string FaturaNo { get; set; } = string.Empty;
    public string ReferansNo { get; set; } = string.Empty;
    public DateTime DuzenlemeTarihi { get; set; } = DateTime.Now;
    
    // Müşteri Bilgileri
    public string MusteriEmail { get; set; } = string.Empty;
    public string MusteriTelefon { get; set; } = string.Empty;
    public string KartSahibi { get; set; } = string.Empty;
    public string KartSon4 { get; set; } = string.Empty;
    public string OdemeSaglayici { get; set; } = "iyzico-sandbox";

    // Hizmet / Kalem Bilgileri
    public string HizmetBasligi { get; set; } = string.Empty;
    public string HizmetTuru { get; set; } = "Araç Kiralama"; // "Araç Kiralama" veya "Uçak Bileti"
    public string DetayBilgisi { get; set; } = string.Empty;
    
    // Tutarlar
    public decimal AraToplam { get; set; }
    public decimal KdvTutari { get; set; }
    public decimal GenelToplam { get; set; }
    public string ParaBirimi { get; set; } = "TRY";
    public string OdemeDurumu { get; set; } = "SUCCESS";
}
// Extra - Invoice PDF END

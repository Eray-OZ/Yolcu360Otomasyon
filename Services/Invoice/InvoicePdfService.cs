// Extra - Invoice PDF START
using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Yolcu360Otomasyon.Models;

namespace Yolcu360Otomasyon.Services;

public sealed class InvoicePdfService
{
    static InvoicePdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<string> GenerateInvoicePdfAsync(InvoiceModel invoice, string? outputDirectory = null)
    {
        var targetDir = outputDirectory;
        if (string.IsNullOrWhiteSpace(targetDir))
        {
            targetDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Downloads");
        }

        Directory.CreateDirectory(targetDir);

        var safeRef = string.Concat(invoice.ReferansNo.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_'));
        if (string.IsNullOrWhiteSpace(safeRef)) safeRef = DateTime.Now.ToString("yyyyMMdd_HHmmss");

        var fileName = $"Fatura_{safeRef}.pdf";
        var fullPath = Path.Combine(targetDir, fileName);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Helvetica"));

                page.Header().Element(c => ComposeHeader(c, invoice));
                page.Content().Element(c => ComposeContent(c, invoice));
                page.Footer().Element(ComposeFooter);
            });
        });

        await Task.Run(() => document.GeneratePdf(fullPath));
        return fullPath;
    }

    private static void ComposeHeader(IContainer container, InvoiceModel invoice)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(titleCol =>
                {
                    titleCol.Item().Text("YOLCU360 OTOMASYON")
                        .FontSize(22)
                        .Bold()
                        .FontColor("#0F172A");

                    titleCol.Item().Text("Elektronik Rezervasyon & Ödeme Dekontu")
                        .FontSize(11)
                        .FontColor("#64748B");
                });

                row.RelativeItem().AlignRight().Column(statusCol =>
                {
                    statusCol.Item().Background("#DCFCE7").PaddingVertical(6).PaddingHorizontal(14).Text("✓ ÖDENDİ (PAID)")
                        .FontSize(12)
                        .Bold()
                        .FontColor("#15803D");

                    statusCol.Item().PaddingTop(4).Text($"Tarih: {invoice.DuzenlemeTarihi:dd.MM.yyyy HH:mm}")
                        .FontSize(9)
                        .FontColor("#64748B");
                });
            });

            col.Item().PaddingVertical(14).LineHorizontal(1).LineColor("#E2E8F0");
        });
    }

    private static void ComposeContent(IContainer container, InvoiceModel invoice)
    {
        container.Column(col =>
        {
            // Bilgi Kutuları (Fatura & Müşteri)
            col.Item().Row(row =>
            {
                // Sol: Fatura Detayları
                row.RelativeItem().Border(1).BorderColor("#E2E8F0").Background("#F8FAFC").Padding(12).Column(leftCol =>
                {
                    leftCol.Item().Text("FATURA BİLGİLERİ").FontSize(11).Bold().FontColor("#0F172A");
                    leftCol.Item().PaddingTop(4).Text($"Fatura No: {invoice.FaturaNo}").FontSize(10);
                    leftCol.Item().Text($"iyzico Referans: {invoice.ReferansNo}").FontSize(10).Bold().FontColor("#0284C7");
                    leftCol.Item().Text($"Ödeme Sağlayıcı: {invoice.OdemeSaglayici}").FontSize(9).FontColor("#64748B");
                    if (!string.IsNullOrWhiteSpace(invoice.KartSon4))
                    {
                        leftCol.Item().Text($"Ödeme Kartı: **** **** **** {invoice.KartSon4}").FontSize(9).FontColor("#64748B");
                    }
                });

                row.ConstantItem(16);

                // Sağ: Müşteri Bilgileri
                row.RelativeItem().Border(1).BorderColor("#E2E8F0").Background("#F8FAFC").Padding(12).Column(rightCol =>
                {
                    rightCol.Item().Text("MÜŞTERİ BİLGİLERİ").FontSize(11).Bold().FontColor("#0F172A");
                    rightCol.Item().PaddingTop(4).Text($"Kart Sahibi: {invoice.KartSahibi}").FontSize(10);
                    rightCol.Item().Text($"E-Posta: {invoice.MusteriEmail}").FontSize(10);
                    rightCol.Item().Text($"Telefon: {invoice.MusteriTelefon}").FontSize(10);
                });
            });

            col.Item().PaddingVertical(18);

            // Tablo Başlığı
            col.Item().Text("HİZMET DETAYLARI").FontSize(12).Bold().FontColor("#0F172A");
            col.Item().PaddingTop(6);

            // Hizmet Tablosu
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(5);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(2);
                });

                table.Header(header =>
                {
                    header.Cell().Background("#0F172A").Padding(8).Text("Hizmet / Açıklama").Bold().FontColor(Colors.White);
                    header.Cell().Background("#0F172A").Padding(8).Text("Tür").Bold().FontColor(Colors.White);
                    header.Cell().Background("#0F172A").Padding(8).AlignRight().Text("KDV").Bold().FontColor(Colors.White);
                    header.Cell().Background("#0F172A").Padding(8).AlignRight().Text("Tutar").Bold().FontColor(Colors.White);
                });

                table.Cell().BorderBottom(1).BorderColor("#E2E8F0").Padding(8).Text(invoice.HizmetBasligi).FontSize(10);
                table.Cell().BorderBottom(1).BorderColor("#E2E8F0").Padding(8).Text(invoice.HizmetTuru).FontSize(10);
                table.Cell().BorderBottom(1).BorderColor("#E2E8F0").Padding(8).AlignRight().Text("%20").FontSize(10);
                table.Cell().BorderBottom(1).BorderColor("#E2E8F0").Padding(8).AlignRight().Text($"{invoice.GenelToplam:N2} {invoice.ParaBirimi}").FontSize(10).Bold();
            });

            col.Item().PaddingVertical(12);

            // Toplamlar Alanı
            col.Item().AlignRight().Width(240).Border(1).BorderColor("#E2E8F0").Background("#F8FAFC").Padding(12).Column(sumCol =>
            {
                sumCol.Item().Row(r =>
                {
                    r.RelativeItem().Text("Ara Toplam:").FontSize(10);
                    r.RelativeItem().AlignRight().Text($"{invoice.AraToplam:N2} {invoice.ParaBirimi}").FontSize(10);
                });

                sumCol.Item().PaddingTop(3).Row(r =>
                {
                    r.RelativeItem().Text("KDV (%20):").FontSize(10);
                    r.RelativeItem().AlignRight().Text($"{invoice.KdvTutari:N2} {invoice.ParaBirimi}").FontSize(10);
                });

                sumCol.Item().PaddingVertical(6).LineHorizontal(1).LineColor("#E2E8F0");

                sumCol.Item().Row(r =>
                {
                    r.RelativeItem().Text("GENEL TOPLAM:").FontSize(11).Bold().FontColor("#0F172A");
                    r.RelativeItem().AlignRight().Text($"{invoice.GenelToplam:N2} {invoice.ParaBirimi}").FontSize(12).Bold().FontColor("#0284C7");
                });
            });

            col.Item().PaddingVertical(20);

            // Önemli Bilgilendirme Notu
            col.Item().Background("#EFF6FF").Border(1).BorderColor("#BFDBFE").Padding(10).Row(noteRow =>
            {
                noteRow.RelativeItem().Column(nCol =>
                {
                    nCol.Item().Text("ℹ️ Bilgilendirme ve Rezervasyon Notu:").FontSize(9).Bold().FontColor("#1E40AF");
                    nCol.Item().PaddingTop(2).Text("Bu belge Yolcu360 Otomasyon sistemi üzerinden gerçekleştirilen rezervasyon ödemesinin resmi elektronik faturası ve dekontudur. Araç teslimi veya uçuş biniş işlemlerinde kimlik belgeniz ve bu dekont numaranız geçerlidir.").FontSize(8).FontColor("#1E3A8A");
                });
            });
        });
    }

    private static void ComposeFooter(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().LineHorizontal(1).LineColor("#E2E8F0");
            col.Item().PaddingTop(6).Row(row =>
            {
                row.RelativeItem().Text("Yolcu360 Otomasyon Sistemi | 7/24 Destek: destek@yolcu360.com").FontSize(8).FontColor("#94A3B8");
                row.RelativeItem().AlignRight().Text("Sayfa 1 / 1").FontSize(8).FontColor("#94A3B8");
            });
        });
    }
}
// Extra - Invoice PDF END

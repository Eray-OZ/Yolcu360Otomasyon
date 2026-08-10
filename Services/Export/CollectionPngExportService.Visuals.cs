using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Yolcu360Otomasyon.Models;

namespace Yolcu360Otomasyon.Services;

public sealed partial class CollectionPngExportService
{
    private static void RenderControlToPng(Control report, string filePath, double width)
    {
        report.Measure(new Size(width, double.PositiveInfinity));
        report.Arrange(new Rect(0, 0, width, report.DesiredSize.Height));

        var pixelWidth = Math.Max(1, (int)Math.Ceiling(report.Bounds.Width));
        var pixelHeight = Math.Max(1, (int)Math.Ceiling(report.Bounds.Height));

        using var bitmap = new RenderTargetBitmap(new PixelSize(pixelWidth, pixelHeight), new Vector(96, 96));
        bitmap.Render(report);

        using var stream = File.Create(filePath);
        bitmap.Save(stream, PngBitmapEncoderOptions.Default);
    }

    private static Control BuildCollectionReportVisual(List<(KoleksiyonListItem Collection, List<SearchResultItem> Vehicles)> items)
    {
        var collections = items.Select(x => x.Collection).ToList();
        var container = new StackPanel { Spacing = 18 };

        container.Children.Add(BuildReportHeader(items, collections));

        foreach (var (collection, vehicles) in items)
            container.Children.Add(BuildSingleCollectionSummaryCard(collection, vehicles));

        return new Border
        {
            Width = CollectionReportWidth,
            Background = new SolidColorBrush(Color.Parse("#F4F7FB")),
            Padding = new Thickness(28),
            Child = container
        };
    }

    private static Control BuildReportHeader(
        List<(KoleksiyonListItem Collection, List<SearchResultItem> Vehicles)> items,
        List<KoleksiyonListItem> collections)
    {
        return new Border
        {
            Background = new SolidColorBrush(Color.Parse("#0F172A")),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(24),
            Child = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock
                    {
                        Text = collections.Count == 1 ? collections[0].OzelAd : $"{collections.Count} Seçili Kayıt",
                        FontSize = 30,
                        FontWeight = FontWeight.Bold,
                        Foreground = Brushes.White
                    },
                    new TextBlock
                    {
                        Text = $"Alış Yeri: {string.Join(", ", collections.Select(item => item.AlisYeri).Distinct())}",
                        Foreground = new SolidColorBrush(Color.Parse("#D6E2F0"))
                    },
                    new TextBlock
                    {
                        Text = collections.Count == 1
                            ? $"Tarih: {collections[0].AlisTarihi:dd.MM.yyyy} {collections[0].AlisSaati} - {collections[0].DonusTarihi:dd.MM.yyyy} {collections[0].DonusSaati}"
                            : $"Tarih Aralığı: {collections.Min(item => item.AlisTarihi):dd.MM.yyyy} - {collections.Max(item => item.DonusTarihi):dd.MM.yyyy}",
                        Foreground = new SolidColorBrush(Color.Parse("#D6E2F0"))
                    },
                    new TextBlock
                    {
                        Text =
                            $"Filtreler: Vites = {string.Join(", ", collections.Select(item => FormatFilterValue(item.SecilenVitesFiltresi)).Distinct())}, " +
                            $"Yakıt = {string.Join(", ", collections.Select(item => FormatFilterValue(item.SecilenYakitFiltresi)).Distinct())}",
                        Foreground = new SolidColorBrush(Color.Parse("#D6E2F0"))
                    },
                    new TextBlock
                    {
                        Text = $"Toplam Araç Sayısı: {items.Sum(x => x.Vehicles.Count)} | Oluşturulma: {collections.Min(item => item.OlusturmaTarihi).ToLocalTime():dd.MM.yyyy HH:mm}",
                        Foreground = new SolidColorBrush(Color.Parse("#D6E2F0"))
                    }
                }
            }
        };
    }
}

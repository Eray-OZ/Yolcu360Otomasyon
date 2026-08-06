using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Yolcu360Otomasyon.Models;

namespace Yolcu360Otomasyon;

public partial class MainWindow : Window
{
    private async Task<string> ExportHistorySelectionAsPngAsync(IReadOnlyList<KoleksiyonListItem> collections)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            HistoryStatusTextBlock.Text = collections.Count == 1
                ? $"{collections[0].OzelAd} PNG olarak hazırlanıyor..."
                : $"{collections.Count} kayıt için PNG hazırlanıyor...";
        });

        var downloadsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads");

        Directory.CreateDirectory(downloadsDirectory);

        var baseName = collections.Count == 1 ? collections[0].OzelAd : $"{collections.Count}_kayit";
        var safeName = string.Concat(baseName.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_'));
        var filePath = Path.Combine(downloadsDirectory, $"{safeName}_{DateTime.Now:yyyyMMdd_HHmmss}.png");

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            const double reportWidth = 1440;
            var report = BuildCollectionReportVisual(collections);
            report.Measure(new Size(reportWidth, double.PositiveInfinity));
            report.Arrange(new Rect(0, 0, reportWidth, report.DesiredSize.Height));

            var width = Math.Max(1, (int)Math.Ceiling(report.Bounds.Width));
            var height = Math.Max(1, (int)Math.Ceiling(report.Bounds.Height));

            using var bitmap = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
            bitmap.Render(report);

            using var stream = File.Create(filePath);
            bitmap.Save(stream, PngBitmapEncoderOptions.Default);
        });

        return filePath;
    }

    private static Control BuildCollectionReportVisual(IReadOnlyList<KoleksiyonListItem> collections)
    {
        var root = new Border
        {
            Width = 1440,
            Background = new SolidColorBrush(Color.Parse("#F4F7FB")),
            Padding = new Thickness(28)
        };

        var container = new StackPanel
        {
            Spacing = 18
        };

        container.Children.Add(new Border
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
                        Text = $"Araç Sayısı: {collections.Sum(item => item.AracSayisi)} | Oluşturulma: {collections.Min(item => item.OlusturmaTarihi).ToLocalTime():dd.MM.yyyy HH:mm}",
                        Foreground = new SolidColorBrush(Color.Parse("#D6E2F0"))
                    }
                }
            }
        });

        foreach (var collection in collections)
            container.Children.Add(BuildSingleCollectionSummaryCard(collection));

        root.Child = container;
        return root;
    }

    private static Control BuildSingleCollectionSummaryCard(KoleksiyonListItem collection)
    {
        return new Border
        {
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#D9E2EC")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(22),
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,*"),
                RowDefinitions = new RowDefinitions("Auto,Auto,Auto"),
                ColumnSpacing = 18,
                RowSpacing = 14,
                Children =
                {
                    CreateSummaryBlock("Kayıt Adı", collection.OzelAd, 0, 0),
                    CreateSummaryBlock("Alış Yeri", collection.AlisYeri, 0, 1),
                    CreateSummaryBlock(
                        "Tarih Aralığı",
                        $"{collection.AlisTarihi:dd.MM.yyyy} {collection.AlisSaati} - {collection.DonusTarihi:dd.MM.yyyy} {collection.DonusSaati}",
                        1,
                        0),
                    CreateSummaryBlock(
                        "Filtreler",
                        $"Vites: {FormatFilterValue(collection.SecilenVitesFiltresi)} | Yakıt: {FormatFilterValue(collection.SecilenYakitFiltresi)}",
                        1,
                        1),
                    CreateSummaryBlock("Araç Sayısı", collection.AracSayisi.ToString(), 2, 0),
                    CreateSummaryBlock("Oluşturulma", collection.OlusturmaTarihi.ToLocalTime().ToString("dd.MM.yyyy HH:mm"), 2, 1)
                }
            }
        };
    }

    private static Control CreateSummaryBlock(string title, string value, int row, int column)
    {
        var panel = new StackPanel
        {
            Spacing = 4,
            Children =
            {
                new TextBlock
                {
                    Text = title,
                    FontWeight = FontWeight.Bold,
                    Foreground = new SolidColorBrush(Color.Parse("#122033"))
                },
                new TextBlock
                {
                    Text = string.IsNullOrWhiteSpace(value) ? "-" : value,
                    Foreground = new SolidColorBrush(Color.Parse("#132235")),
                    TextWrapping = TextWrapping.Wrap
                }
            }
        };

        Grid.SetRow(panel, row);
        Grid.SetColumn(panel, column);
        return panel;
    }

    private static string FormatFilterValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "Farketmez" : value;
    }
}

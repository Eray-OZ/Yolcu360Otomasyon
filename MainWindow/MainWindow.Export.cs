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

        var collectionsWithVehicles = new List<(KoleksiyonListItem Collection, List<SearchResultItem> Vehicles)>();
        foreach (var collection in collections)
        {
            var vehicles = await _databaseService.GetCollectionVehiclesAsync(collection.Id);
            collectionsWithVehicles.Add((collection, vehicles));
        }

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
            var report = BuildCollectionReportVisual(collectionsWithVehicles);
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

    private static Control BuildCollectionReportVisual(List<(KoleksiyonListItem Collection, List<SearchResultItem> Vehicles)> items)
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

        var collections = items.Select(x => x.Collection).ToList();

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
                            $"Filtreler: Vites = {string.Join(", ", collections.Select(item => FormatFilterValue(item.SecilenVitesFiltresi, isTransmission: true)).Distinct())}, " +
                            $"Yakıt = {string.Join(", ", collections.Select(item => FormatFilterValue(item.SecilenYakitFiltresi, isTransmission: false)).Distinct())}",
                        Foreground = new SolidColorBrush(Color.Parse("#D6E2F0"))
                    },
                    new TextBlock
                    {
                        Text = $"Toplam Araç Sayısı: {items.Sum(x => x.Vehicles.Count)} | Oluşturulma: {collections.Min(item => item.OlusturmaTarihi).ToLocalTime():dd.MM.yyyy HH:mm}",
                        Foreground = new SolidColorBrush(Color.Parse("#D6E2F0"))
                    }
                }
            }
        });

        foreach (var (collection, vehicles) in items)
            container.Children.Add(BuildSingleCollectionSummaryCard(collection, vehicles));

        root.Child = container;
        return root;
    }

    private static Control BuildSingleCollectionSummaryCard(KoleksiyonListItem collection, List<SearchResultItem> vehicles)
    {
        var container = new StackPanel { Spacing = 14 };

        var headerCard = new Border
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
                        $"Vites: {FormatFilterValue(collection.SecilenVitesFiltresi, isTransmission: true)} | Yakıt: {FormatFilterValue(collection.SecilenYakitFiltresi, isTransmission: false)}",
                        1,
                        1),
                    CreateSummaryBlock("Araç Sayısı", vehicles.Count.ToString(), 2, 0),
                    CreateSummaryBlock("Oluşturulma", collection.OlusturmaTarihi.ToLocalTime().ToString("dd.MM.yyyy HH:mm"), 2, 1)
                }
            }
        };
        container.Children.Add(headerCard);

        if (vehicles.Count > 0)
        {
            var vehiclesCard = new Border
            {
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.Parse("#D9E2EC")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(20),
                Child = new StackPanel
                {
                    Spacing = 12,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = $"Koleksiyondaki Tüm Araçlar ({vehicles.Count})",
                            FontSize = 18,
                            FontWeight = FontWeight.Bold,
                            Foreground = new SolidColorBrush(Color.Parse("#0F172A"))
                        },
                        BuildVehiclesGrid(vehicles)
                    }
                }
            };
            container.Children.Add(vehiclesCard);
        }

        return container;
    }

    private static Control BuildVehiclesGrid(List<SearchResultItem> vehicles)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("2*,2*,1*,1*,1*,1*,1*"),
            RowSpacing = 8,
            ColumnSpacing = 12
        };

        var rowDefs = new RowDefinitions();
        rowDefs.Add(new RowDefinition(GridLength.Auto));
        foreach (var _ in vehicles)
            rowDefs.Add(new RowDefinition(GridLength.Auto));
        grid.RowDefinitions = rowDefs;

        AddTableCell(grid, "Araç Adı", 0, 0, isHeader: true);
        AddTableCell(grid, "Detay", 0, 1, isHeader: true);
        AddTableCell(grid, "Fiyat", 0, 2, isHeader: true);
        AddTableCell(grid, "Günlük Fiyat", 0, 3, isHeader: true);
        AddTableCell(grid, "Vites", 0, 4, isHeader: true);
        AddTableCell(grid, "Yakıt", 0, 5, isHeader: true);
        AddTableCell(grid, "Tedarikçi", 0, 6, isHeader: true);

        for (int i = 0; i < vehicles.Count; i++)
        {
            var v = vehicles[i];
            var row = i + 1;
            AddTableCell(grid, v.Title, row, 0);
            AddTableCell(grid, v.Subtitle, row, 1);
            AddTableCell(grid, v.Price, row, 2, isHighlight: true);
            AddTableCell(grid, v.DailyPrice, row, 3);
            AddTableCell(grid, v.Transmission, row, 4);
            AddTableCell(grid, v.FuelType, row, 5);
            AddTableCell(grid, v.Supplier, row, 6);
        }

        return grid;
    }

    private static void AddTableCell(Grid grid, string? text, int row, int col, bool isHeader = false, bool isHighlight = false)
    {
        var tb = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(text) ? "-" : text,
            FontWeight = isHeader ? FontWeight.Bold : (isHighlight ? FontWeight.SemiBold : FontWeight.Normal),
            Foreground = isHeader
                ? new SolidColorBrush(Color.Parse("#0F172A"))
                : (isHighlight ? new SolidColorBrush(Color.Parse("#2563EB")) : new SolidColorBrush(Color.Parse("#334155"))),
            FontSize = isHeader ? 14 : 13,
            TextWrapping = TextWrapping.Wrap
        };
        Grid.SetRow(tb, row);
        Grid.SetColumn(tb, col);
        grid.Children.Add(tb);
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

    private static string FormatFilterValue(string? value, bool isTransmission = false)
    {
        if (string.IsNullOrWhiteSpace(value) || value == "Farketmez")
            return "-";

        return value;
    }
}

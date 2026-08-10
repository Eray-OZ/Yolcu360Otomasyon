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
    private const double CollectionReportWidth = 1440;

    private async Task<string> ExportHistorySelectionAsPngAsync(IReadOnlyList<KoleksiyonListItem> collections)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            SetHistoryStatus(collections.Count == 1
                ? $"{collections[0].OzelAd} PNG olarak hazırlanıyor..."
                : $"{collections.Count} kayıt için PNG hazırlanıyor...");
        });

        var collectionsWithVehicles = await LoadCollectionsWithVehiclesAsync(collections);
        var filePath = BuildHistoryExportPath(collections);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var report = BuildCollectionReportVisual(collectionsWithVehicles);
            RenderControlToPng(report, filePath, CollectionReportWidth);
        });

        return filePath;
    }

    private async Task<List<(KoleksiyonListItem Collection, List<SearchResultItem> Vehicles)>> LoadCollectionsWithVehiclesAsync(
        IReadOnlyList<KoleksiyonListItem> collections)
    {
        var tasks = collections.Select(async collection =>
        {
            var vehicles = await _databaseService.GetCollectionVehiclesAsync(collection.Id);
            return (Collection: collection, Vehicles: vehicles);
        });

        return (await Task.WhenAll(tasks)).ToList();
    }

    private static string BuildHistoryExportPath(IReadOnlyList<KoleksiyonListItem> collections)
    {
        var downloadsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads");

        Directory.CreateDirectory(downloadsDirectory);

        var baseName = collections.Count == 1 ? collections[0].OzelAd : $"{collections.Count}_kayit";
        var safeName = string.Concat(baseName.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_'));
        return Path.Combine(downloadsDirectory, $"{safeName}_{DateTime.Now:yyyyMMdd_HHmmss}.png");
    }

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
                        Text = $"Toplam Araç Sayısı: {items.Sum(x => x.Vehicles.Count)} | Oluşturulma: {collections.Min(item => item.OlusturmaTarihi).ToLocalTime():dd.MM.yyyy HH:mm}",
                        Foreground = new SolidColorBrush(Color.Parse("#D6E2F0"))
                    }
                }
            }
        });

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
                        $"Vites: {FormatFilterValue(collection.SecilenVitesFiltresi)} | Yakıt: {FormatFilterValue(collection.SecilenYakitFiltresi)}",
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

        for (int i = 0; i <= vehicles.Count; i++)
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

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

    private static string FormatFilterValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value == "Farketmez")
            return "-";

        return value;
    }
}

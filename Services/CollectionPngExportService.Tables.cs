using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Yolcu360Otomasyon.Models;

namespace Yolcu360Otomasyon.Services;

public sealed partial class CollectionPngExportService
{
    private static Control BuildVehiclesGrid(List<SearchResultItem> vehicles)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("2*,2*,1*,1*,1*,1*,1*"),
            RowSpacing = 8,
            ColumnSpacing = 12
        };

        for (var i = 0; i <= vehicles.Count; i++)
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        AddTableCell(grid, "Araç Adı", 0, 0, isHeader: true);
        AddTableCell(grid, "Detay", 0, 1, isHeader: true);
        AddTableCell(grid, "Fiyat", 0, 2, isHeader: true);
        AddTableCell(grid, "Günlük Fiyat", 0, 3, isHeader: true);
        AddTableCell(grid, "Vites", 0, 4, isHeader: true);
        AddTableCell(grid, "Yakıt", 0, 5, isHeader: true);
        AddTableCell(grid, "Tedarikçi", 0, 6, isHeader: true);

        for (var i = 0; i < vehicles.Count; i++)
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

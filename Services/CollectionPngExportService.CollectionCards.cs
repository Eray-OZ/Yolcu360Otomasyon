using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Yolcu360Otomasyon.Models;

namespace Yolcu360Otomasyon.Services;

public sealed partial class CollectionPngExportService
{
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
}

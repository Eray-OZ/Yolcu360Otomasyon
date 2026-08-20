// Extra - Car Comparison START
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Yolcu360Otomasyon.Models;
using Yolcu360Otomasyon.Services;

namespace Yolcu360Otomasyon;

public partial class MainWindow : Window
{
    private readonly CarComparisonService _carComparisonService = new();
    private List<CarComparisonItem> _currentComparisonItems = new();
    private string _comparisonPreviousSection = "Search";

    private void CompareResultsButton_Click(object? sender, RoutedEventArgs e)
    {
        var selectedList = ResultsDataGrid.SelectedItems.OfType<SearchResultItem>().ToList();
        if (selectedList.Count < 2)
        {
            if (ResultsDataGrid.SelectedItem is SearchResultItem singleItem)
            {
                selectedList = new List<SearchResultItem> { singleItem };
            }
        }

        if (selectedList.Count > 3)
        {
            SearchResultsEmptyTextBlock.Text = $"⚠️ Karşılaştırma için en fazla 3 araç seçebilirsiniz (Şu an {selectedList.Count} araç seçili). Lütfen seçiminizi 2 veya 3 araçla sınırlandırın.";
            SearchResultsEmptyTextBlock.IsVisible = true;
            return;
        }

        if (selectedList.Count < 2)
        {
            SearchResultsEmptyTextBlock.Text = "⚠️ Karşılaştırma yapmak için lütfen listeden en az 2 (en fazla 3) araç seçin (Ctrl/Cmd tuşuna basılı tutarak seçebilirsiniz).";
            SearchResultsEmptyTextBlock.IsVisible = true;
            return;
        }

        SearchResultsEmptyTextBlock.IsVisible = false;
        OpenCarComparison(selectedList, "Arama Sonuçları", "Search");
    }

    private void CompareHistoryVehiclesButton_Click(object? sender, RoutedEventArgs e)
    {
        var selectedList = CollectionVehiclesDataGrid.SelectedItems.OfType<SearchResultItem>().ToList();
        if (selectedList.Count < 2)
        {
            if (CollectionVehiclesDataGrid.SelectedItem is SearchResultItem singleItem)
            {
                selectedList = new List<SearchResultItem> { singleItem };
            }
        }

        if (selectedList.Count > 3)
        {
            VehicleStatusTextBlock.Text = $"⚠️ Karşılaştırma için en fazla 3 araç seçebilirsiniz (Şu an {selectedList.Count} araç seçili). Lütfen seçiminizi 2 veya 3 araçla sınırlandırın.";
            return;
        }

        if (selectedList.Count < 2)
        {
            VehicleStatusTextBlock.Text = "⚠️ Karşılaştırma yapmak için lütfen koleksiyondan en az 2 (en fazla 3) araç seçin (Ctrl/Cmd tuşuna basılı tutarak seçebilirsiniz).";
            return;
        }

        var colName = _selectedCollection?.OzelAd ?? "Kayıtlı Koleksiyon";
        OpenCarComparison(selectedList, $"Koleksiyon: {colName}", "History");
    }

    private void OpenCarComparison(List<SearchResultItem> vehicles, string sourceTitle, string returnSection)
    {
        _comparisonPreviousSection = returnSection;
        _currentComparisonItems = _carComparisonService.BuildComparison(vehicles, sourceTitle);

        if (_currentComparisonItems.Count < 2)
            return;

        CarComparisonSubtitleTextBlock.Text = $"{sourceTitle} içerisinden seçilen {_currentComparisonItems.Count} aracın detaylı karşılaştırması";
        CarComparisonStatusTextBlock.Text = $"{_currentComparisonItems.Count} araç başarıyla karşılaştırıldı. En uygun fiyatlı seçenek yeşil rozetle işaretlendi.";

        // Kart 1
        PopulateComparisonCard(1, _currentComparisonItems[0]);

        // Kart 2
        PopulateComparisonCard(2, _currentComparisonItems[1]);

        // Kart 3 (Varsa)
        if (_currentComparisonItems.Count >= 3)
        {
            ComparisonCard3.IsVisible = true;
            PopulateComparisonCard(3, _currentComparisonItems[2]);
        }
        else
        {
            ComparisonCard3.IsVisible = false;
        }

        ShowComparisonSection();
    }

    private void PopulateComparisonCard(int cardIndex, CarComparisonItem item)
    {
        var supplier = string.IsNullOrWhiteSpace(item.Vehicle.Supplier) ? "Tedarikçi Belirtilmedi" : item.Vehicle.Supplier;
        var title = string.IsNullOrWhiteSpace(item.Vehicle.Title) ? "Araç Modeli" : item.Vehicle.Title;
        var price = string.IsNullOrWhiteSpace(item.Vehicle.Price) ? $"{item.TotalPriceNumeric:N0} TL" : item.Vehicle.Price;
        var dailyPrice = string.IsNullOrWhiteSpace(item.Vehicle.DailyPrice) ? $"{item.DailyPriceNumeric:N0} TL / gün" : item.Vehicle.DailyPrice;
        var transmission = string.IsNullOrWhiteSpace(item.Vehicle.Transmission) ? "Belirtilmedi" : item.Vehicle.Transmission;
        var fuel = string.IsNullOrWhiteSpace(item.Vehicle.FuelType) ? "Belirtilmedi" : item.Vehicle.FuelType;
        var pickup = string.IsNullOrWhiteSpace(item.Vehicle.PickupInfo) ? "Ofis Teslimi" : item.Vehicle.PickupInfo;
        var advantages = item.AdvantageBadges.Count > 0 ? string.Join(Environment.NewLine, item.AdvantageBadges.Select(b => $"• {b}")) : "• Standart Kiralama";

        if (cardIndex == 1)
        {
            CompCard1SupplierText.Text = supplier;
            CompCard1TitleText.Text = title;
            CompCard1PriceText.Text = price;
            CompCard1DailyPriceText.Text = dailyPrice;
            CompCard1TransmissionText.Text = transmission;
            CompCard1FuelText.Text = fuel;
            CompCard1PickupText.Text = pickup;
            CompCard1AdvantagesText.Text = advantages;

            CompCard1BadgeText.Text = item.PriceBadgeText;
            CompCard1BadgeBorder.Background = Brush.Parse(item.IsCheapest ? "#DCFCE7" : "#F1F5F9");
            CompCard1BadgeText.Foreground = Brush.Parse(item.IsCheapest ? "#15803D" : "#64748B");
        }
        else if (cardIndex == 2)
        {
            CompCard2SupplierText.Text = supplier;
            CompCard2TitleText.Text = title;
            CompCard2PriceText.Text = price;
            CompCard2DailyPriceText.Text = dailyPrice;
            CompCard2TransmissionText.Text = transmission;
            CompCard2FuelText.Text = fuel;
            CompCard2PickupText.Text = pickup;
            CompCard2AdvantagesText.Text = advantages;

            CompCard2BadgeText.Text = item.PriceBadgeText;
            CompCard2BadgeBorder.Background = Brush.Parse(item.IsCheapest ? "#DCFCE7" : "#F1F5F9");
            CompCard2BadgeText.Foreground = Brush.Parse(item.IsCheapest ? "#15803D" : "#64748B");
        }
        else if (cardIndex == 3)
        {
            CompCard3SupplierText.Text = supplier;
            CompCard3TitleText.Text = title;
            CompCard3PriceText.Text = price;
            CompCard3DailyPriceText.Text = dailyPrice;
            CompCard3TransmissionText.Text = transmission;
            CompCard3FuelText.Text = fuel;
            CompCard3PickupText.Text = pickup;
            CompCard3AdvantagesText.Text = advantages;

            CompCard3BadgeText.Text = item.PriceBadgeText;
            CompCard3BadgeBorder.Background = Brush.Parse(item.IsCheapest ? "#DCFCE7" : "#F1F5F9");
            CompCard3BadgeText.Foreground = Brush.Parse(item.IsCheapest ? "#15803D" : "#64748B");
        }
    }

    private void CloseComparisonButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_comparisonPreviousSection == "History")
        {
            ShowHistorySection();
        }
        else
        {
            ShowSearchSection();
        }
    }

    private void CompCard1PayButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_currentComparisonItems.Count > 0)
            ProceedToPaymentWithComparisonVehicle(_currentComparisonItems[0].Vehicle);
    }

    private void CompCard2PayButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_currentComparisonItems.Count > 1)
            ProceedToPaymentWithComparisonVehicle(_currentComparisonItems[1].Vehicle);
    }

    private void CompCard3PayButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_currentComparisonItems.Count > 2)
            ProceedToPaymentWithComparisonVehicle(_currentComparisonItems[2].Vehicle);
    }

    private void ProceedToPaymentWithComparisonVehicle(SearchResultItem vehicle)
    {
        if (_activeUser is null)
        {
            CarComparisonStatusTextBlock.Text = "Ödeme yapmak için lütfen önce giriş yapın.";
            return;
        }

        var price = ParseVehiclePrice(vehicle.Price);
        var title = string.IsNullOrWhiteSpace(vehicle.Title) ? "Kiralık Araç" : vehicle.Title;
        var colName = _selectedCollection?.OzelAd ?? "Araç Kiralama";

        _paymentPreviewItems = new List<OdemeHazirlikItem>
        {
            new OdemeHazirlikItem
            {
                KoleksiyonId = _selectedCollection?.Id,
                KoleksiyonAdi = $"{colName} ({title})",
                Tutar = price
            }
        };

        _lastPaidFlight = null;
        PrepareCheckoutSummary();
        ShowPaymentCheckoutSection();
    }
}
// Extra - Car Comparison END

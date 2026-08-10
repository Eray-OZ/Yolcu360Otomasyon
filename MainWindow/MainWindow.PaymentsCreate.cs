using Avalonia.Interactivity;
using Yolcu360Otomasyon.Models;
using Yolcu360Otomasyon.Services;

namespace Yolcu360Otomasyon;

public partial class MainWindow
{
    private void CreatePaymentButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_activeUser is null || _selectedCollection is null)
        {
            SetHistoryStatus("Ödeme oluşturmak için lütfen bir koleksiyon seçin.");
            return;
        }

        var vehicle = _selectedVehicle ?? _selectedCollectionVehicles.FirstOrDefault();
        if (vehicle is null)
        {
            SetHistoryStatus("Ödeme yapmak için lütfen koleksiyon içerisinden bir araç seçin.");
            return;
        }

        CreatePaymentButton.IsEnabled = false;
        try
        {
            var vehiclePrice = ParseVehiclePrice(vehicle.Price);

            _paymentPreviewItems = new List<OdemeHazirlikItem>
            {
                new OdemeHazirlikItem
                {
                    KoleksiyonId = _selectedCollection.Id,
                    KoleksiyonAdi = $"{_selectedCollection.OzelAd} ({vehicle.Title})",
                    Tutar = vehiclePrice
                }
            };

            PrepareCheckoutSummary();
            ShowPaymentCheckoutSection();
        }
        catch (Exception ex)
        {
            SetHistoryStatus($"Ödeme oluşturma hatası: {ex.Message}");
        }
        finally
        {
            CreatePaymentButton.IsEnabled = true;
        }
    }

    private static decimal ParseVehiclePrice(string? priceText)
    {
        var parsed = DatabaseService.ParseCurrency(priceText ?? string.Empty);
        return parsed > 0 ? parsed : 100.00m;
    }
}

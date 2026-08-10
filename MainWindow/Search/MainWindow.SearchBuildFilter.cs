using System.Globalization;
using Avalonia.Controls;
using Yolcu360Otomasyon.Models;

namespace Yolcu360Otomasyon;

public partial class MainWindow
{
    private bool TryBuildSearchFilter(out SearchFilter filter)
    {
        filter = new SearchFilter();

        if (!DateTime.TryParseExact(
                PickupDateTextBoxControl.Text?.Trim(),
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var pickupDate)
            || !DateTime.TryParseExact(
                ReturnDateTextBoxControl.Text?.Trim(),
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var returnDate))
        {
            SetSearchStatus("Tarih formatı gecersiz. Ornek: 2026-08-10");
            return false;
        }

        filter = new SearchFilter
        {
            PickupLocation = PickupLocationTextBoxControl.Text?.Trim() ?? string.Empty,
            PickupDate = pickupDate.Date,
            ReturnDate = returnDate.Date,
            PickupTime = PickupTimeTextBoxControl.Text?.Trim() ?? "10:00",
            ReturnTime = ReturnTimeTextBoxControl.Text?.Trim() ?? "18:00",
            TransmissionType = GetComboBoxTag(TransmissionComboBoxControl),
            FuelType = GetComboBoxTag(FuelComboBoxControl)
        };
        _latestSearchFilter = filter;

        if (!string.IsNullOrWhiteSpace(filter.PickupLocation))
            return true;

        SetSearchStatus("Alış yeri boş olamaz.");
        return false;
    }

    private static string GetComboBoxTag(ComboBox comboBox)
    {
        return (comboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? string.Empty;
    }
}

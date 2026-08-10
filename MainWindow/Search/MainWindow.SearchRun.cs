using Avalonia.Interactivity;

namespace Yolcu360Otomasyon;

public partial class MainWindow
{
    private async void SearchButton_Click(object? sender, RoutedEventArgs e)
    {
        SearchButtonControl.IsEnabled = false;
        SetSearchStatus("Arama hazırlanıyor...");

        try
        {
            if (!TryBuildSearchFilter(out var filter))
                return;

            if (_activeUser is null)
            {
                SetSearchStatus("Önce giriş yapılmalı.");
                return;
            }

            var results = await RunEmbeddedSearchAsync(filter);
            await DisplaySearchResultsAsync(results);
            await Task.Delay(800);
        }
        catch (Exception ex)
        {
            SetSearchStatus($"Arama hatası: {ex.Message}");
        }
        finally
        {
            SearchButtonControl.IsEnabled = true;
            ShowSearchSection();
        }
    }
}

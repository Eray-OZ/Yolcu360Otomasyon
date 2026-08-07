using Avalonia.Controls;
using Avalonia.Controls.Selection;
using Avalonia.Interactivity;
using Yolcu360Otomasyon.Models;

namespace Yolcu360Otomasyon;

public partial class MainWindow : Window
{
    private async void HistoryTabButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_isAuthenticating) return;
        ShowHistorySection();
        await LoadHistoryAsync();
    }

    private async void CollectionsDataGrid_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _selectedCollections = CollectionsDataGrid.SelectedItems?.OfType<KoleksiyonListItem>().ToList()
            ?? (CollectionsDataGrid.SelectedItem is KoleksiyonListItem single ? [single] : new List<KoleksiyonListItem>());

        if (_selectedCollections.Count == 0)
        {
            _selectedCollection = null;
            _selectedVehicle = null;
            _selectedCollectionVehicles = new List<SearchResultItem>();
            CollectionVehiclesDataGrid.ItemsSource = null;
            ClearSelectedCollectionSummary();
            return;
        }

        _selectedCollection = _selectedCollections[0];
        UpdateSelectedCollectionSummary(_selectedCollections);
        HistoryStatusTextBlock.Text = _selectedCollections.Count == 1
            ? $"{_selectedCollection.OzelAd} kaydı seçildi."
            : $"{_selectedCollections.Count} kayıt seçildi.";
    }

    private async void ViewVehiclesButton_Click(object? sender, RoutedEventArgs e)
    {
        await OpenSelectedCollectionVehiclesAsync();
    }

    private async void CollectionsDataGrid_DoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        await OpenSelectedCollectionVehiclesAsync();
    }

    private async Task OpenSelectedCollectionVehiclesAsync()
    {
        if (_selectedCollection is null)
        {
            HistoryStatusTextBlock.Text = "Araçlarını görüntülemek için lütfen bir koleksiyon seçin.";
            return;
        }

        _selectedCollectionVehicles = await _databaseService.GetCollectionVehiclesAsync(_selectedCollection.Id);
        CollectionVehiclesDataGrid.ItemsSource = null;
        CollectionVehiclesDataGrid.ItemsSource = _selectedCollectionVehicles;

        if (_selectedCollectionVehicles.Count > 0)
        {
            CollectionVehiclesDataGrid.SelectedItem = _selectedCollectionVehicles[0];
            _selectedVehicle = _selectedCollectionVehicles[0];
        }
        else
        {
            _selectedVehicle = null;
        }

        VehicleViewTitleTextBlock.Text = $"{_selectedCollection.OzelAd} (Araç Listesi)";
        VehicleViewSubtitleTextBlock.Text = $"Alış Yeri: {_selectedCollection.AlisYeri} | Toplam {_selectedCollectionVehicles.Count} Araç Kayıtlı";
        VehicleStatusTextBlock.Text = $"{_selectedCollection.OzelAd} koleksiyonu için {_selectedCollectionVehicles.Count} araç listelendi.";

        CollectionsViewPanel.IsVisible = false;
        VehiclesViewPanel.IsVisible = true;
    }

    private void BackToCollectionsButton_Click(object? sender, RoutedEventArgs e)
    {
        VehiclesViewPanel.IsVisible = false;
        CollectionsViewPanel.IsVisible = true;
        HistoryStatusTextBlock.Text = "Koleksiyonlar listesine dönüldü.";
    }

    private void CollectionVehiclesDataGrid_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (CollectionVehiclesDataGrid.SelectedItem is SearchResultItem vehicle)
        {
            _selectedVehicle = vehicle;
            VehicleStatusTextBlock.Text = $"{_selectedCollection?.OzelAd} - {vehicle.Title} seçildi ({vehicle.Price}).";
        }
    }

    private async void DeleteCollectionButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_activeUser is null || _selectedCollections.Count == 0)
        {
            HistoryStatusTextBlock.Text = "Silmek için bir kayıt seçin.";
            return;
        }

        DeleteCollectionButton.IsEnabled = false;
        try
        {
            foreach (var collection in _selectedCollections)
                await _databaseService.DeleteCollectionAsync(collection.Id, _activeUser.Id);

            _selectedCollection = null;
            _selectedCollections = new List<KoleksiyonListItem>();
            _selectedCollectionVehicles = new List<SearchResultItem>();
            ClearSelectedCollectionSummary();
            await LoadHistoryAsync();
            HistoryStatusTextBlock.Text = "Seçili kayıtlar silindi.";
        }
        catch (Exception ex)
        {
            HistoryStatusTextBlock.Text = $"Silme hatası: {ex.Message}";
        }
        finally
        {
            DeleteCollectionButton.IsEnabled = true;
        }
    }

    private async void ExportPngButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedCollections.Count == 0)
        {
            HistoryStatusTextBlock.Text = "PNG indirmek için bir kayıt seçin.";
            return;
        }

        ExportPngButton.IsEnabled = false;
        try
        {
            var filePath = await ExportHistorySelectionAsPngAsync(_selectedCollections);
            HistoryStatusTextBlock.Text = $"PNG kaydedildi: {filePath}";
        }
        catch (Exception ex)
        {
            HistoryStatusTextBlock.Text = $"PNG oluşturma hatası: {ex.Message}";
        }
        finally
        {
            ExportPngButton.IsEnabled = true;
        }
    }

    private async Task LoadHistoryAsync()
    {
        if (_activeUser is null)
            return;

        CollectionsViewPanel.IsVisible = true;
        VehiclesViewPanel.IsVisible = false;

        var collections = await _databaseService.GetCollectionsAsync(_activeUser.Id);
        CollectionsDataGrid.ItemsSource = null;
        CollectionsDataGrid.ItemsSource = collections;

        if (collections.Count == 0)
        {
            _selectedCollection = null;
            _selectedCollections = new List<KoleksiyonListItem>();
            _selectedCollectionVehicles = new List<SearchResultItem>();
            ClearSelectedCollectionSummary();
            HistoryStatusTextBlock.Text = "Kayıt bulunamadı.";
            return;
        }

        if (_selectedCollection is null || collections.All(item => item.Id != _selectedCollection.Id))
            CollectionsDataGrid.SelectedItem = collections[0];

        HistoryStatusTextBlock.Text = $"{collections.Count} kayıt listelendi.";
    }

    private void UpdateSelectedCollectionSummary(IReadOnlyList<KoleksiyonListItem> collections)
    {
        if (collections.Count == 1)
        {
            var collection = collections[0];
            SelectedCollectionNameTextBlock.Text = collection.OzelAd;
            SelectedCollectionLocationTextBlock.Text = collection.AlisYeri;
            SelectedCollectionDateRangeTextBlock.Text =
                $"{collection.AlisTarihi:dd.MM.yyyy} {collection.AlisSaati} - {collection.DonusTarihi:dd.MM.yyyy} {collection.DonusSaati}";

            var transmission = string.IsNullOrWhiteSpace(collection.SecilenVitesFiltresi)
                ? "Farketmez"
                : collection.SecilenVitesFiltresi;
            var fuel = string.IsNullOrWhiteSpace(collection.SecilenYakitFiltresi)
                ? "Farketmez"
                : collection.SecilenYakitFiltresi;
            SelectedCollectionFiltersTextBlock.Text = $"Vites: {transmission} | Yakıt: {fuel}";
            SelectedCollectionCountTextBlock.Text = collection.AracSayisi.ToString();
            SelectedCollectionCreatedAtTextBlock.Text = collection.OlusturmaTarihi.ToLocalTime().ToString("dd.MM.yyyy HH:mm");
            return;
        }

        SelectedCollectionNameTextBlock.Text = $"{collections.Count} kayıt seçildi";
        SelectedCollectionLocationTextBlock.Text = string.Join(", ", collections.Select(item => item.AlisYeri).Distinct());
        SelectedCollectionDateRangeTextBlock.Text =
            $"{collections.Min(item => item.AlisTarihi):dd.MM.yyyy} - {collections.Max(item => item.DonusTarihi):dd.MM.yyyy}";
        SelectedCollectionFiltersTextBlock.Text =
            $"Vites: {string.Join(", ", collections.Select(item => string.IsNullOrWhiteSpace(item.SecilenVitesFiltresi) ? "Farketmez" : item.SecilenVitesFiltresi).Distinct())} | " +
            $"Yakıt: {string.Join(", ", collections.Select(item => string.IsNullOrWhiteSpace(item.SecilenYakitFiltresi) ? "Farketmez" : item.SecilenYakitFiltresi).Distinct())}";
        SelectedCollectionCountTextBlock.Text = collections.Sum(item => item.AracSayisi).ToString();
        SelectedCollectionCreatedAtTextBlock.Text =
            $"{collections.Min(item => item.OlusturmaTarihi).ToLocalTime():dd.MM.yyyy HH:mm} - {collections.Max(item => item.OlusturmaTarihi).ToLocalTime():dd.MM.yyyy HH:mm}";
    }

    private void ClearSelectedCollectionSummary()
    {
        SelectedCollectionNameTextBlock.Text = "-";
        SelectedCollectionLocationTextBlock.Text = "-";
        SelectedCollectionDateRangeTextBlock.Text = "-";
        SelectedCollectionFiltersTextBlock.Text = "-";
        SelectedCollectionCountTextBlock.Text = "-";
        SelectedCollectionCreatedAtTextBlock.Text = "-";
    }
}

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

    // Extra - Dynamic Collections START
    private async void RefreshSelectedCollectionButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_activeUser is null)
        {
            HistoryStatusTextBlock.Text = "Güncelleme için önce giriş yapılmalı.";
            return;
        }

        if (_selectedCollection is null || _selectedCollections.Count != 1)
        {
            HistoryStatusTextBlock.Text = "Güncellemek için tek bir koleksiyon seçin.";
            return;
        }

        SetCollectionRefreshButtonsEnabled(false);
        SetCollectionRefreshButtonText("Güncelleniyor...");
        var collection = _selectedCollection;
        var wasVehiclesViewVisible = VehiclesViewPanel.IsVisible;

        try
        {
            HistoryStatusTextBlock.Text = $"{collection.OzelAd} güncelleniyor...";
            VehicleStatusTextBlock.Text = $"{collection.OzelAd} için güncel araçlar getiriliyor...";
            KeepBrowserAliveOffscreen();

            var baService = CreateBAService();
            baService.ProgressChanged += message =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    HistoryStatusTextBlock.Text = message;
                    VehicleStatusTextBlock.Text = message;
                });
            };

            var refreshedVehicles = await _dynamicCollectionService.RefreshSnapshotAsync(
                _activeUser.Id,
                collection.Id,
                baService,
                _activeUser.SessionStatePath);

            _selectedCollectionVehicles = refreshedVehicles;
            _selectedVehicle = refreshedVehicles.FirstOrDefault();
            CollectionVehiclesDataGrid.ItemsSource = null;
            CollectionVehiclesDataGrid.ItemsSource = _selectedCollectionVehicles;
            CollectionVehiclesDataGrid.SelectedItem = _selectedVehicle;

            await LoadHistoryAsync();

            var collections = (CollectionsDataGrid.ItemsSource as IEnumerable<KoleksiyonListItem>)?.ToList()
                ?? new List<KoleksiyonListItem>();
            var refreshedCollection = collections.FirstOrDefault(item => item.Id == collection.Id);
            if (refreshedCollection is not null)
            {
                CollectionsDataGrid.SelectedItem = refreshedCollection;
                _selectedCollection = refreshedCollection;
                UpdateSelectedCollectionSummary([refreshedCollection]);
            }

            VehicleViewTitleTextBlock.Text = $"{collection.OzelAd} (Araç Listesi)";
            VehicleViewSubtitleTextBlock.Text = $"Alış Yeri: {collection.AlisYeri} | Toplam {refreshedVehicles.Count} Araç Kayıtlı";
            HistoryStatusTextBlock.Text = $"{collection.OzelAd} güncellendi. {refreshedVehicles.Count} araç kaydedildi.";
            VehicleStatusTextBlock.Text = $"{collection.OzelAd} güncellendi. {refreshedVehicles.Count} araç listelendi.";
            ShowHistorySection();
            CollectionsViewPanel.IsVisible = !wasVehiclesViewVisible;
            VehiclesViewPanel.IsVisible = wasVehiclesViewVisible;
        }
        catch (Exception ex)
        {
            HistoryStatusTextBlock.Text = $"Güncelleme hatası: {ex.Message}";
            VehicleStatusTextBlock.Text = $"Güncelleme hatası: {ex.Message}";
            ShowHistorySection();
        }
        finally
        {
            SetCollectionRefreshButtonsEnabled(true);
            SetCollectionRefreshButtonText("Güncelle");
        }
    }
    // Extra - Dynamic Collections END

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

        VehicleViewTitleTextBlock.Text = _selectedCollection.OzelAd;
        VehicleViewSubtitleTextBlock.Text = $"Alış Yeri: {_selectedCollection.AlisYeri}";
        VehicleStatusTextBlock.Text = $"{_selectedCollectionVehicles.Count} araç listelendi.";

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

    // Extra - Dynamic Collections START
    private void SetCollectionRefreshButtonsEnabled(bool enabled)
    {
        RefreshCollectionButton.IsEnabled = enabled;
        RefreshCollectionButtonVehicles.IsEnabled = enabled;
        ViewVehiclesButton.IsEnabled = enabled;
        DeleteCollectionButton.IsEnabled = enabled;
        ExportPngButton.IsEnabled = enabled;
        ExportPngButtonVehicles.IsEnabled = enabled;
        // Extra - Collection Export START
        ExportCsvButton.IsEnabled = enabled;
        ExportCsvButtonVehicles.IsEnabled = enabled;
        ExportExcelButton.IsEnabled = enabled;
        ExportExcelButtonVehicles.IsEnabled = enabled;
        // Extra - Collection Export END
    }

    private void SetCollectionRefreshButtonText(string text)
    {
        RefreshCollectionButton.Content = text;
        RefreshCollectionButtonVehicles.Content = text;
    }
    // Extra - Dynamic Collections END

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

    // Extra - Collection Export START
    private async void ExportCsvButton_Click(object? sender, RoutedEventArgs e)
    {
        await ExportHistorySelectionAsync(
            "CSV indirmek için bir kayıt seçin.",
            "CSV",
            items => _collectionExportService.ExportCsv(items));
    }

    private async void ExportExcelButton_Click(object? sender, RoutedEventArgs e)
    {
        await ExportHistorySelectionAsync(
            "Excel indirmek için bir kayıt seçin.",
            "Excel",
            items => _collectionExportService.ExportExcel(items));
    }

    private async Task ExportHistorySelectionAsync(
        string emptyMessage,
        string format,
        Func<IReadOnlyList<(KoleksiyonListItem Collection, List<SearchResultItem> Vehicles)>, string> exporter)
    {
        if (_selectedCollections.Count == 0)
        {
            HistoryStatusTextBlock.Text = emptyMessage;
            return;
        }

        SetCollectionExportButtonsEnabled(false);
        try
        {
            var tasks = _selectedCollections.Select(async collection =>
            {
                var vehicles = await _databaseService.GetCollectionVehiclesAsync(collection.Id);
                return (Collection: collection, Vehicles: vehicles);
            });
            var items = (await Task.WhenAll(tasks)).ToList();
            var path = exporter(items);
            HistoryStatusTextBlock.Text = $"{format} kaydedildi: {path}";
        }
        catch (Exception ex)
        {
            HistoryStatusTextBlock.Text = $"{format} oluşturma hatası: {ex.Message}";
        }
        finally
        {
            SetCollectionExportButtonsEnabled(true);
        }
    }

    private void SetCollectionExportButtonsEnabled(bool enabled)
    {
        ExportPngButton.IsEnabled = enabled;
        ExportPngButtonVehicles.IsEnabled = enabled;
        ExportCsvButton.IsEnabled = enabled;
        ExportCsvButtonVehicles.IsEnabled = enabled;
        ExportExcelButton.IsEnabled = enabled;
        ExportExcelButtonVehicles.IsEnabled = enabled;
    }
    // Extra - Collection Export END

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
            HistoryStatusTextBlock.Text = $"{collection.OzelAd} ({collection.AracSayisi} araç)";
            return;
        }

        HistoryStatusTextBlock.Text = collections.Count > 1
            ? $"{collections.Count} kayıt seçildi."
            : $"{collections.Count} kayıt listelendi.";
    }

    private void ClearSelectedCollectionSummary()
    {
    }
}

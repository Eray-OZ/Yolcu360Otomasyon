using Avalonia.Controls;
using Avalonia.Controls.Selection;
using Avalonia.Interactivity;
using Yolcu360Otomasyon.Models;

namespace Yolcu360Otomasyon;

public partial class MainWindow : Window
{
    private Border HistoryPanel => HistoryViewControl.FindControl<Border>("HistoryPanel")!;
    private Grid CollectionsViewPanel => HistoryViewControl.FindControl<Grid>("CollectionsViewPanel")!;
    private Grid VehiclesViewPanel => HistoryViewControl.FindControl<Grid>("VehiclesViewPanel")!;
    private TextBlock HistoryStatusTextBlock => HistoryViewControl.FindControl<TextBlock>("HistoryStatusTextBlock")!;
    private DataGrid CollectionsDataGrid => HistoryViewControl.FindControl<DataGrid>("CollectionsDataGrid")!;
    private Button ViewVehiclesButton => HistoryViewControl.FindControl<Button>("ViewVehiclesButton")!;
    private Button DeleteCollectionButton => HistoryViewControl.FindControl<Button>("DeleteCollectionButton")!;
    private Button ExportPngButton => HistoryViewControl.FindControl<Button>("ExportPngButton")!;
    private TextBlock SelectedCollectionNameTextBlock => HistoryViewControl.FindControl<TextBlock>("SelectedCollectionNameTextBlock")!;
    private TextBlock SelectedCollectionLocationTextBlock => HistoryViewControl.FindControl<TextBlock>("SelectedCollectionLocationTextBlock")!;
    private TextBlock SelectedCollectionDateRangeTextBlock => HistoryViewControl.FindControl<TextBlock>("SelectedCollectionDateRangeTextBlock")!;
    private TextBlock SelectedCollectionFiltersTextBlock => HistoryViewControl.FindControl<TextBlock>("SelectedCollectionFiltersTextBlock")!;
    private TextBlock SelectedCollectionCountTextBlock => HistoryViewControl.FindControl<TextBlock>("SelectedCollectionCountTextBlock")!;
    private TextBlock SelectedCollectionCreatedAtTextBlock => HistoryViewControl.FindControl<TextBlock>("SelectedCollectionCreatedAtTextBlock")!;
    private TextBlock VehicleViewTitleTextBlock => HistoryViewControl.FindControl<TextBlock>("VehicleViewTitleTextBlock")!;
    private TextBlock VehicleViewSubtitleTextBlock => HistoryViewControl.FindControl<TextBlock>("VehicleViewSubtitleTextBlock")!;
    private Button BackToCollectionsButton => HistoryViewControl.FindControl<Button>("BackToCollectionsButton")!;
    private TextBlock VehicleStatusTextBlock => HistoryViewControl.FindControl<TextBlock>("VehicleStatusTextBlock")!;
    private DataGrid CollectionVehiclesDataGrid => HistoryViewControl.FindControl<DataGrid>("CollectionVehiclesDataGrid")!;
    private Button BackToCollectionsButtonBottom => HistoryViewControl.FindControl<Button>("BackToCollectionsButtonBottom")!;
    private Button CreatePaymentButton => HistoryViewControl.FindControl<Button>("CreatePaymentButton")!;
    private Button ExportPngButtonVehicles => HistoryViewControl.FindControl<Button>("ExportPngButtonVehicles")!;

    private void ConfigureHistoryViewEvents()
    {
        CollectionsDataGrid.SelectionChanged += CollectionsDataGrid_SelectionChanged;
        CollectionsDataGrid.DoubleTapped += CollectionsDataGrid_DoubleTapped;
        ViewVehiclesButton.Click += ViewVehiclesButton_Click;
        DeleteCollectionButton.Click += DeleteCollectionButton_Click;
        ExportPngButton.Click += ExportPngButton_Click;
        BackToCollectionsButton.Click += BackToCollectionsButton_Click;
        BackToCollectionsButtonBottom.Click += BackToCollectionsButton_Click;
        CollectionVehiclesDataGrid.SelectionChanged += CollectionVehiclesDataGrid_SelectionChanged;
        CreatePaymentButton.Click += CreatePaymentButton_Click;
        ExportPngButtonVehicles.Click += ExportPngButton_Click;
    }

    private async void HistoryTabButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_isAuthenticating) return;
        ShowHistorySection();
        await LoadHistoryAsync();
    }

    private async void CollectionsDataGrid_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        SetSelectedCollectionsFromGrid();

        if (_selectedCollections.Count == 0)
        {
            ClearSelectedCollectionState();
            return;
        }

        _selectedCollection = _selectedCollections[0];
        UpdateSelectedCollectionSummary(_selectedCollections);
        SetHistoryStatus(_selectedCollections.Count == 1
            ? $"{_selectedCollection.OzelAd} kaydı seçildi."
            : $"{_selectedCollections.Count} kayıt seçildi.");
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
            SetHistoryStatus("Araçlarını görüntülemek için lütfen bir koleksiyon seçin.");
            return;
        }

        var vehicles = await _databaseService.GetCollectionVehiclesAsync(_selectedCollection.Id);
        DisplayCollectionVehicles(_selectedCollection, vehicles);
        SetVehicleStatus($"{_selectedCollection.OzelAd} koleksiyonu için {_selectedCollectionVehicles.Count} araç listelendi.");

        CollectionsViewPanel.IsVisible = false;
        VehiclesViewPanel.IsVisible = true;
    }

    private void BackToCollectionsButton_Click(object? sender, RoutedEventArgs e)
    {
        VehiclesViewPanel.IsVisible = false;
        CollectionsViewPanel.IsVisible = true;
        SetHistoryStatus("Koleksiyonlar listesine dönüldü.");
    }

    private void CollectionVehiclesDataGrid_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (CollectionVehiclesDataGrid.SelectedItem is SearchResultItem vehicle)
        {
            _selectedVehicle = vehicle;
            SetVehicleStatus($"{_selectedCollection?.OzelAd} - {vehicle.Title} seçildi ({vehicle.Price}).");
        }
    }

    private async void DeleteCollectionButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_activeUser is null || _selectedCollections.Count == 0)
        {
            SetHistoryStatus("Silmek için bir kayıt seçin.");
            return;
        }

        DeleteCollectionButton.IsEnabled = false;
        try
        {
            foreach (var collection in _selectedCollections)
                await _databaseService.DeleteCollectionAsync(collection.Id, _activeUser.Id);

            ClearSelectedCollectionState();
            await LoadHistoryAsync();
            SetHistoryStatus("Seçili kayıtlar silindi.");
        }
        catch (Exception ex)
        {
            SetHistoryStatus($"Silme hatası: {ex.Message}");
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
            SetHistoryStatus("PNG indirmek için bir kayıt seçin.");
            return;
        }

        ExportPngButton.IsEnabled = false;
        try
        {
            var filePath = await ExportHistorySelectionAsPngAsync(_selectedCollections);
            SetHistoryStatus($"PNG kaydedildi: {filePath}");
        }
        catch (Exception ex)
        {
            SetHistoryStatus($"PNG oluşturma hatası: {ex.Message}");
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
            ClearSelectedCollectionState();
            SetHistoryStatus("Kayıt bulunamadı.");
            return;
        }

        if (_selectedCollection is null || collections.All(item => item.Id != _selectedCollection.Id))
            CollectionsDataGrid.SelectedItem = collections[0];

        SetHistoryStatus($"{collections.Count} kayıt listelendi.");
    }

    private void SetSelectedCollectionsFromGrid()
    {
        _selectedCollections = CollectionsDataGrid.SelectedItems?.OfType<KoleksiyonListItem>().ToList()
            ?? (CollectionsDataGrid.SelectedItem is KoleksiyonListItem single ? [single] : new List<KoleksiyonListItem>());
    }

    private void ClearSelectedCollectionState()
    {
        _selectedCollection = null;
        _selectedVehicle = null;
        _selectedCollections = new List<KoleksiyonListItem>();
        _selectedCollectionVehicles = new List<SearchResultItem>();
        CollectionVehiclesDataGrid.ItemsSource = null;
        ClearSelectedCollectionSummary();
    }

    private void DisplayCollectionVehicles(KoleksiyonListItem collection, List<SearchResultItem> vehicles)
    {
        _selectedCollectionVehicles = vehicles;
        CollectionVehiclesDataGrid.ItemsSource = null;
        CollectionVehiclesDataGrid.ItemsSource = _selectedCollectionVehicles;
        SelectFirstVehicleIfAny();

        VehicleViewTitleTextBlock.Text = $"{collection.OzelAd} (Araç Listesi)";
        VehicleViewSubtitleTextBlock.Text = $"Alış Yeri: {collection.AlisYeri} | Toplam {_selectedCollectionVehicles.Count} Araç Kayıtlı";
    }

    private void SelectFirstVehicleIfAny()
    {
        if (_selectedCollectionVehicles.Count == 0)
        {
            _selectedVehicle = null;
            return;
        }

        CollectionVehiclesDataGrid.SelectedItem = _selectedCollectionVehicles[0];
        _selectedVehicle = _selectedCollectionVehicles[0];
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

            var transmission = string.IsNullOrWhiteSpace(collection.SecilenVitesFiltresi) || collection.SecilenVitesFiltresi == "Farketmez"
                ? "-"
                : collection.SecilenVitesFiltresi;
            var fuel = string.IsNullOrWhiteSpace(collection.SecilenYakitFiltresi) || collection.SecilenYakitFiltresi == "Farketmez"
                ? "-"
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
            $"Vites: {string.Join(", ", collections.Select(item => string.IsNullOrWhiteSpace(item.SecilenVitesFiltresi) || item.SecilenVitesFiltresi == "Farketmez" ? "-" : item.SecilenVitesFiltresi).Distinct())} | " +
            $"Yakıt: {string.Join(", ", collections.Select(item => string.IsNullOrWhiteSpace(item.SecilenYakitFiltresi) || item.SecilenYakitFiltresi == "Farketmez" ? "-" : item.SecilenYakitFiltresi).Distinct())}";
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

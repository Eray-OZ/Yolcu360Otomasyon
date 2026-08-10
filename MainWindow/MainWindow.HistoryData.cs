using Yolcu360Otomasyon.Models;

namespace Yolcu360Otomasyon;

public partial class MainWindow
{
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

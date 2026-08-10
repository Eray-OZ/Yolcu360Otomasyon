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

        CollectionsViewPanelControl.IsVisible = false;
        VehiclesViewPanelControl.IsVisible = true;
    }

    private async Task LoadHistoryAsync()
    {
        if (_activeUser is null)
            return;

        CollectionsViewPanelControl.IsVisible = true;
        VehiclesViewPanelControl.IsVisible = false;

        var collections = await _databaseService.GetCollectionsAsync(_activeUser.Id);
        CollectionsDataGridControl.ItemsSource = null;
        CollectionsDataGridControl.ItemsSource = collections;

        if (collections.Count == 0)
        {
            ClearSelectedCollectionState();
            SetHistoryStatus("Kayıt bulunamadı.");
            return;
        }

        if (_selectedCollection is null || collections.All(item => item.Id != _selectedCollection.Id))
            CollectionsDataGridControl.SelectedItem = collections[0];

        SetHistoryStatus($"{collections.Count} kayıt listelendi.");
    }

    private void SetSelectedCollectionsFromGrid()
    {
        _selectedCollections = CollectionsDataGridControl.SelectedItems?.OfType<KoleksiyonListItem>().ToList()
            ?? (CollectionsDataGridControl.SelectedItem is KoleksiyonListItem single ? [single] : new List<KoleksiyonListItem>());
    }

    private void ClearSelectedCollectionState()
    {
        _selectedCollection = null;
        _selectedVehicle = null;
        _selectedCollections = new List<KoleksiyonListItem>();
        _selectedCollectionVehicles = new List<SearchResultItem>();
        CollectionVehiclesDataGridControl.ItemsSource = null;
        ClearSelectedCollectionSummary();
    }

    private void DisplayCollectionVehicles(KoleksiyonListItem collection, List<SearchResultItem> vehicles)
    {
        _selectedCollectionVehicles = vehicles;
        CollectionVehiclesDataGridControl.ItemsSource = null;
        CollectionVehiclesDataGridControl.ItemsSource = _selectedCollectionVehicles;
        SelectFirstVehicleIfAny();

        VehicleViewTitleTextBlockControl.Text = $"{collection.OzelAd} (Araç Listesi)";
        VehicleViewSubtitleTextBlockControl.Text = $"Alış Yeri: {collection.AlisYeri} | Toplam {_selectedCollectionVehicles.Count} Araç Kayıtlı";
    }

    private void SelectFirstVehicleIfAny()
    {
        if (_selectedCollectionVehicles.Count == 0)
        {
            _selectedVehicle = null;
            return;
        }

        CollectionVehiclesDataGridControl.SelectedItem = _selectedCollectionVehicles[0];
        _selectedVehicle = _selectedCollectionVehicles[0];
    }

    private void UpdateSelectedCollectionSummary(IReadOnlyList<KoleksiyonListItem> collections)
    {
        if (collections.Count == 1)
        {
            var collection = collections[0];
            SelectedCollectionNameTextBlockControl.Text = collection.OzelAd;
            SelectedCollectionLocationTextBlockControl.Text = collection.AlisYeri;
            SelectedCollectionDateRangeTextBlockControl.Text =
                $"{collection.AlisTarihi:dd.MM.yyyy} {collection.AlisSaati} - {collection.DonusTarihi:dd.MM.yyyy} {collection.DonusSaati}";

            var transmission = string.IsNullOrWhiteSpace(collection.SecilenVitesFiltresi) || collection.SecilenVitesFiltresi == "Farketmez"
                ? "-"
                : collection.SecilenVitesFiltresi;
            var fuel = string.IsNullOrWhiteSpace(collection.SecilenYakitFiltresi) || collection.SecilenYakitFiltresi == "Farketmez"
                ? "-"
                : collection.SecilenYakitFiltresi;
            SelectedCollectionFiltersTextBlockControl.Text = $"Vites: {transmission} | Yakıt: {fuel}";
            SelectedCollectionCountTextBlockControl.Text = collection.AracSayisi.ToString();
            SelectedCollectionCreatedAtTextBlockControl.Text = collection.OlusturmaTarihi.ToLocalTime().ToString("dd.MM.yyyy HH:mm");
            return;
        }

        SelectedCollectionNameTextBlockControl.Text = $"{collections.Count} kayıt seçildi";
        SelectedCollectionLocationTextBlockControl.Text = string.Join(", ", collections.Select(item => item.AlisYeri).Distinct());
        SelectedCollectionDateRangeTextBlockControl.Text =
            $"{collections.Min(item => item.AlisTarihi):dd.MM.yyyy} - {collections.Max(item => item.DonusTarihi):dd.MM.yyyy}";
        SelectedCollectionFiltersTextBlockControl.Text =
            $"Vites: {string.Join(", ", collections.Select(item => string.IsNullOrWhiteSpace(item.SecilenVitesFiltresi) || item.SecilenVitesFiltresi == "Farketmez" ? "-" : item.SecilenVitesFiltresi).Distinct())} | " +
            $"Yakıt: {string.Join(", ", collections.Select(item => string.IsNullOrWhiteSpace(item.SecilenYakitFiltresi) || item.SecilenYakitFiltresi == "Farketmez" ? "-" : item.SecilenYakitFiltresi).Distinct())}";
        SelectedCollectionCountTextBlockControl.Text = collections.Sum(item => item.AracSayisi).ToString();
        SelectedCollectionCreatedAtTextBlockControl.Text =
            $"{collections.Min(item => item.OlusturmaTarihi).ToLocalTime():dd.MM.yyyy HH:mm} - {collections.Max(item => item.OlusturmaTarihi).ToLocalTime():dd.MM.yyyy HH:mm}";
    }

    private void ClearSelectedCollectionSummary()
    {
        SelectedCollectionNameTextBlockControl.Text = "-";
        SelectedCollectionLocationTextBlockControl.Text = "-";
        SelectedCollectionDateRangeTextBlockControl.Text = "-";
        SelectedCollectionFiltersTextBlockControl.Text = "-";
        SelectedCollectionCountTextBlockControl.Text = "-";
        SelectedCollectionCreatedAtTextBlockControl.Text = "-";
    }
}

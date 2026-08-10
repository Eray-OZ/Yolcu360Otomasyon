using Avalonia.Controls;
using Avalonia.Controls.Selection;
using Avalonia.Interactivity;

namespace Yolcu360Otomasyon;

public partial class MainWindow
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

    private void BackToCollectionsButton_Click(object? sender, RoutedEventArgs e)
    {
        VehiclesViewPanel.IsVisible = false;
        CollectionsViewPanel.IsVisible = true;
        SetHistoryStatus("Koleksiyonlar listesine dönüldü.");
    }

    private void CollectionVehiclesDataGrid_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (CollectionVehiclesDataGrid.SelectedItem is Models.SearchResultItem vehicle)
        {
            _selectedVehicle = vehicle;
            SetVehicleStatus($"{_selectedCollection?.OzelAd} - {vehicle.Title} seçildi ({vehicle.Price}).");
        }
    }
}

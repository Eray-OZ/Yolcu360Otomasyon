using Avalonia.Controls;
using Avalonia.Controls.Selection;
using Avalonia.Interactivity;

namespace Yolcu360Otomasyon;

public partial class MainWindow
{
    private Border HistoryPanelControl => HistoryViewRootControl.FindControl<Border>("HistoryPanel")!;
    private Grid CollectionsViewPanelControl => HistoryViewRootControl.FindControl<Grid>("CollectionsViewPanel")!;
    private Grid VehiclesViewPanelControl => HistoryViewRootControl.FindControl<Grid>("VehiclesViewPanel")!;
    private TextBlock HistoryStatusTextBlockControl => HistoryViewRootControl.FindControl<TextBlock>("HistoryStatusTextBlock")!;
    private DataGrid CollectionsDataGridControl => HistoryViewRootControl.FindControl<DataGrid>("CollectionsDataGrid")!;
    private Button ViewVehiclesButtonControl => HistoryViewRootControl.FindControl<Button>("ViewVehiclesButton")!;
    private Button DeleteCollectionButtonControl => HistoryViewRootControl.FindControl<Button>("DeleteCollectionButton")!;
    private Button ExportPngButtonControl => HistoryViewRootControl.FindControl<Button>("ExportPngButton")!;
    private TextBlock SelectedCollectionNameTextBlockControl => HistoryViewRootControl.FindControl<TextBlock>("SelectedCollectionNameTextBlock")!;
    private TextBlock SelectedCollectionLocationTextBlockControl => HistoryViewRootControl.FindControl<TextBlock>("SelectedCollectionLocationTextBlock")!;
    private TextBlock SelectedCollectionDateRangeTextBlockControl => HistoryViewRootControl.FindControl<TextBlock>("SelectedCollectionDateRangeTextBlock")!;
    private TextBlock SelectedCollectionFiltersTextBlockControl => HistoryViewRootControl.FindControl<TextBlock>("SelectedCollectionFiltersTextBlock")!;
    private TextBlock SelectedCollectionCountTextBlockControl => HistoryViewRootControl.FindControl<TextBlock>("SelectedCollectionCountTextBlock")!;
    private TextBlock SelectedCollectionCreatedAtTextBlockControl => HistoryViewRootControl.FindControl<TextBlock>("SelectedCollectionCreatedAtTextBlock")!;
    private TextBlock VehicleViewTitleTextBlockControl => HistoryViewRootControl.FindControl<TextBlock>("VehicleViewTitleTextBlock")!;
    private TextBlock VehicleViewSubtitleTextBlockControl => HistoryViewRootControl.FindControl<TextBlock>("VehicleViewSubtitleTextBlock")!;
    private Button BackToCollectionsButtonControl => HistoryViewRootControl.FindControl<Button>("BackToCollectionsButton")!;
    private TextBlock VehicleStatusTextBlockControl => HistoryViewRootControl.FindControl<TextBlock>("VehicleStatusTextBlock")!;
    private DataGrid CollectionVehiclesDataGridControl => HistoryViewRootControl.FindControl<DataGrid>("CollectionVehiclesDataGrid")!;
    private Button BackToCollectionsButtonBottomControl => HistoryViewRootControl.FindControl<Button>("BackToCollectionsButtonBottom")!;
    private Button CreatePaymentButtonControl => HistoryViewRootControl.FindControl<Button>("CreatePaymentButton")!;
    private Button ExportPngButtonVehiclesControl => HistoryViewRootControl.FindControl<Button>("ExportPngButtonVehicles")!;

    private void ConfigureHistoryViewEvents()
    {
        CollectionsDataGridControl.SelectionChanged += CollectionsDataGrid_SelectionChanged;
        CollectionsDataGridControl.DoubleTapped += CollectionsDataGrid_DoubleTapped;
        ViewVehiclesButtonControl.Click += ViewVehiclesButton_Click;
        DeleteCollectionButtonControl.Click += DeleteCollectionButton_Click;
        ExportPngButtonControl.Click += ExportPngButton_Click;
        BackToCollectionsButtonControl.Click += BackToCollectionsButton_Click;
        BackToCollectionsButtonBottomControl.Click += BackToCollectionsButton_Click;
        CollectionVehiclesDataGridControl.SelectionChanged += CollectionVehiclesDataGrid_SelectionChanged;
        CreatePaymentButtonControl.Click += CreatePaymentButton_Click;
        ExportPngButtonVehiclesControl.Click += ExportPngButton_Click;
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
        VehiclesViewPanelControl.IsVisible = false;
        CollectionsViewPanelControl.IsVisible = true;
        SetHistoryStatus("Koleksiyonlar listesine dönüldü.");
    }

    private void CollectionVehiclesDataGrid_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (CollectionVehiclesDataGridControl.SelectedItem is Models.SearchResultItem vehicle)
        {
            _selectedVehicle = vehicle;
            SetVehicleStatus($"{_selectedCollection?.OzelAd} - {vehicle.Title} seçildi ({vehicle.Price}).");
        }
    }
}

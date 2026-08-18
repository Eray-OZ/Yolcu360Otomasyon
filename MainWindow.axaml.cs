using System.Globalization;
using Avalonia.Controls;
using Yolcu360Otomasyon.Configuration;
using Yolcu360Otomasyon.Models;
using Yolcu360Otomasyon.Services;

namespace Yolcu360Otomasyon;

public partial class MainWindow : Window
{
    private readonly DatabaseService _databaseService = new(AppSettings.GetConnectionString());
    // Extra - Collection Export START
    private readonly CollectionExportService _collectionExportService = new();
    // Extra - Collection Export END
    // Extra - Statistics START
    private readonly StatisticsService _statisticsService = new(AppSettings.GetConnectionString());
    // Extra - Statistics END
    // Extra - Dynamic Collections START
    private readonly DynamicCollectionService _dynamicCollectionService;
    // Extra - Dynamic Collections END
    // Extra - Location Suggestion START
    private readonly LocationSuggestionService _locationSuggestionService = new();
    private readonly FlightLocationSuggestionService _flightLocationSuggestionService = new();
    // Extra - Location Suggestion END
    private readonly SmsReceiverService _smsReceiverService = new(5001);
    private readonly IyzicoPaymentService _iyzicoPaymentService;
    private AppUser? _activeUser;
    private List<SearchResultItem> _latestResults = new();
    private List<FlightResultItem> _latestFlightResults = new();
    private List<SearchResultItem> _selectedCollectionVehicles = new();
    private SearchResultItem? _selectedVehicle;
    private KoleksiyonListItem? _selectedCollection;
    private List<KoleksiyonListItem> _selectedCollections = new();
    private List<OdemeHazirlikItem> _paymentPreviewItems = new();
    private SearchFilter? _latestSearchFilter;
    private string _searchResultsPlaceholderText = "Sonuçları görmek için arama yapın.";
    // Extra - Location Suggestion START
    private CancellationTokenSource? _pickupLocationSuggestionCts;
    private int _pickupLocationSuggestionRequestVersion;
    private bool _suppressPickupLocationSuggestionLookup;
    // Extra - Dropoff Location START
    // Separate suggestion state for optional different dropoff location.
    private CancellationTokenSource? _dropoffLocationSuggestionCts;
    private int _dropoffLocationSuggestionRequestVersion;
    private bool _suppressDropoffLocationSuggestionLookup;
    // Extra - Dropoff Location END
    private CancellationTokenSource? _flightFromSuggestionCts;
    private CancellationTokenSource? _flightToSuggestionCts;
    private int _flightFromSuggestionRequestVersion;
    private int _flightToSuggestionRequestVersion;
    private bool _suppressFlightFromSuggestionLookup;
    private bool _suppressFlightToSuggestionLookup;
    private LocationSuggestionItem? _selectedFlightFromSuggestion;
    private LocationSuggestionItem? _selectedFlightToSuggestion;
    // Extra - Location Suggestion END
    private bool _isAuthenticating;

    public MainWindow()
    {
        InitializeComponent();
        // Extra - Dynamic Collections START
        _dynamicCollectionService = new DynamicCollectionService(_databaseService);
        // Extra - Dynamic Collections END
        _iyzicoPaymentService = new IyzicoPaymentService(AppSettings.GetIyzicoSettings());
        // Extra - Statistics START
        ConfigureStatisticsGrids();
        // Extra - Statistics END
        PickupDatePicker.DisplayDateStart = DateTime.Today;
        ReturnDatePicker.DisplayDateStart = DateTime.Today;
        FlightDepartureDatePicker.DisplayDateStart = DateTime.Today;
        FlightReturnDatePicker.DisplayDateStart = DateTime.Today;
        PickupDatePicker.SelectedDate = DateTime.Today;
        ReturnDatePicker.SelectedDate = DateTime.Today.AddDays(2);
        FlightDepartureDatePicker.SelectedDate = DateTime.Today.AddDays(7);
        FlightReturnDatePicker.SelectedDate = null;
        UpdateSearchDateTexts();
        UpdateFlightDateTexts();
        InitializeTimeComboBox(PickupTimeComboBox, "10:00");
        InitializeTimeComboBox(ReturnTimeComboBox, "18:00");
        ConfigureResultsGrid();
        ConfigureFlightResultsGrid();
        ConfigureCollectionsGrid();
        ConfigurePaymentsGrid();
        _smsReceiverService.SmsReceived += SmsReceiverService_SmsReceived;
        _ = _databaseService.EnsureDatabaseAsync();
        InitializeSmsReceiver();

        _activeUser = null;
        ShowLoginView();
    }

    private static void InitializeTimeComboBox(ComboBox comboBox, string selectedTime)
    {
        comboBox.Items.Clear();

        for (var hour = 0; hour < 24; hour++)
        {
            foreach (var minute in new[] { 0, 30 })
            {
                var time = $"{hour:00}:{minute:00}";
                comboBox.Items.Add(new ComboBoxItem
                {
                    Content = time,
                    Tag = time
                });
            }
        }

        comboBox.SelectedItem = comboBox.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(item => item.Tag?.ToString() == selectedTime);
    }

    private void SetNavigationEnabled(bool enabled)
    {
        _isAuthenticating = !enabled;
        SearchTabButton.IsEnabled = enabled;
        HistoryTabButton.IsEnabled = enabled;
        PaymentsTabButton.IsEnabled = enabled;
        // Extra - Statistics START
        StatisticsTabButton.IsEnabled = enabled;
        // Extra - Statistics END
        if (LogoutButton is not null)
            LogoutButton.IsEnabled = enabled;
    }

    private void SetNavigationVisibility(bool visible)
    {
        TopNavigationPanel.IsVisible = visible;
        if (LogoutButton is not null)
            LogoutButton.IsVisible = visible;
    }

    protected override async void OnClosed(EventArgs e)
    {
        // Extra - Location Suggestion START
        _pickupLocationSuggestionCts?.Cancel();
        _pickupLocationSuggestionCts?.Dispose();
        _flightFromSuggestionCts?.Cancel();
        _flightFromSuggestionCts?.Dispose();
        _flightToSuggestionCts?.Cancel();
        _flightToSuggestionCts?.Dispose();
        // Extra - Location Suggestion END
        await _smsReceiverService.DisposeAsync();

        base.OnClosed(e);
    }
}

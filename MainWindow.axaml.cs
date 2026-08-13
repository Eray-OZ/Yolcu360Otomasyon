using System.Globalization;
using Avalonia.Controls;
using Yolcu360Otomasyon.Configuration;
using Yolcu360Otomasyon.Models;
using Yolcu360Otomasyon.Services;

namespace Yolcu360Otomasyon;

public partial class MainWindow : Window
{
    private readonly DatabaseService _databaseService = new(AppSettings.GetConnectionString());
    // Extra - Dynamic Collections START
    private readonly DynamicCollectionService _dynamicCollectionService;
    // Extra - Dynamic Collections END
    // Extra - Location Suggestion START
    private readonly LocationSuggestionService _locationSuggestionService = new();
    // Extra - Location Suggestion END
    private readonly SmsReceiverService _smsReceiverService = new(5001);
    private readonly IyzicoCallbackService _iyzicoCallbackService = new();
    private readonly IyzicoPaymentService _iyzicoPaymentService;
    private AppUser? _activeUser;
    private List<SearchResultItem> _latestResults = new();
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
    private CancellationTokenSource? _flightFromSuggestionCts;
    private CancellationTokenSource? _flightToSuggestionCts;
    private int _flightFromSuggestionRequestVersion;
    private int _flightToSuggestionRequestVersion;
    private bool _suppressFlightFromSuggestionLookup;
    private bool _suppressFlightToSuggestionLookup;
    // Extra - Location Suggestion END
    private bool _isAuthenticating;

    public MainWindow()
    {
        InitializeComponent();
        // Extra - Dynamic Collections START
        _dynamicCollectionService = new DynamicCollectionService(_databaseService);
        // Extra - Dynamic Collections END
        _iyzicoPaymentService = new IyzicoPaymentService(AppSettings.GetIyzicoSettings(), _iyzicoCallbackService);
        PickupDatePicker.DisplayDateStart = DateTime.Today;
        ReturnDatePicker.DisplayDateStart = DateTime.Today;
        PickupDatePicker.SelectedDate = DateTime.Today;
        ReturnDatePicker.SelectedDate = DateTime.Today.AddDays(2);
        UpdateSearchDateTexts();
        InitializeTimeComboBox(PickupTimeComboBox, "10:00");
        InitializeTimeComboBox(ReturnTimeComboBox, "18:00");
        ConfigureResultsGrid();
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
        await _iyzicoCallbackService.DisposeAsync();

        base.OnClosed(e);
    }
}

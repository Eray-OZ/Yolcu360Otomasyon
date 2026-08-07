using System.Globalization;
using Avalonia.Controls;
using Yolcu360Otomasyon.Configuration;
using Yolcu360Otomasyon.Models;
using Yolcu360Otomasyon.Services;

namespace Yolcu360Otomasyon;

public partial class MainWindow : Window
{
    private readonly DatabaseService _databaseService = new(AppSettings.GetConnectionString());
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
    private bool _isAuthenticating;

    public MainWindow()
    {
        InitializeComponent();
        _iyzicoPaymentService = new IyzicoPaymentService(AppSettings.GetIyzicoSettings(), _iyzicoCallbackService);
        PickupDateTextBox.Text = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        ReturnDateTextBox.Text = DateTime.Today.AddDays(2).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        PickupTimeTextBox.Text = "10:00";
        ReturnTimeTextBox.Text = "18:00";
        ConfigureResultsGrid();
        ConfigureCollectionsGrid();
        ConfigurePaymentsGrid();
        _smsReceiverService.SmsReceived += SmsReceiverService_SmsReceived;
        _ = _databaseService.EnsureDatabaseAsync();
        InitializeSmsReceiver();

        _activeUser = null;
        ShowLoginView();
    }

    private void SetNavigationEnabled(bool enabled)
    {
        _isAuthenticating = !enabled;
        SearchTabButton.IsEnabled = enabled;
        HistoryTabButton.IsEnabled = enabled;
        PaymentsTabButton.IsEnabled = enabled;
        NativeWebViewTestButton.IsEnabled = enabled;
        if (LogoutButton is not null)
            LogoutButton.IsEnabled = enabled;
    }

    private void SetNavigationVisibility(bool visible)
    {
        TopNavigationPanel.IsVisible = visible;
        if (LogoutButton is not null)
            LogoutButton.IsVisible = visible;
    }
}

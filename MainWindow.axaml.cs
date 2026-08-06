using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Controls.Selection;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Yolcu360Otomasyon.Configuration;
using Yolcu360Otomasyon.Models;
using Yolcu360Otomasyon.Services;

namespace Yolcu360Otomasyon;

public partial class MainWindow : Window
{
    private BrowserAutomationService? _browserAutomationService;
    private readonly DatabaseService _databaseService = new(AppSettings.GetConnectionString());
    private readonly SmsReceiverService _smsReceiverService = new();
    private AppUser? _activeUser;
    private List<SearchResultItem> _latestResults = new();
    private List<SearchResultItem> _selectedCollectionVehicles = new();
    private KoleksiyonListItem? _selectedCollection;

    public MainWindow()
    {
        InitializeComponent();
        PickupDateTextBox.Text = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        ReturnDateTextBox.Text = DateTime.Today.AddDays(2).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        PickupTimeTextBox.Text = "10:00";
        ReturnTimeTextBox.Text = "18:00";
        ConfigureResultsGrid();
        ConfigureCollectionsGrid();
        ConfigureHistoryVehiclesGrid();
        _smsReceiverService.SmsReceived += SmsReceiverService_SmsReceived;
        _ = _databaseService.EnsureDatabaseAsync();
        InitializeSmsReceiver();
    }

    private async void InitializeSmsReceiver()
    {
        try
        {
            await _smsReceiverService.StartAsync();
            StatusTextBlock.Text = $"SMS alıcısı hazır. URL: http://192.168.1.161:{_smsReceiverService.Port}/sms";
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = $"SMS alıcısı başlatılamadı: {ex.Message}";
        }
    }

    private async void LoginButton_Click(object? sender, RoutedEventArgs e)
    {
        var email = LoginEmailTextBox.Text?.Trim() ?? string.Empty;
        var password = LoginPasswordTextBox.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            StatusTextBlock.Text = "Email ve şifre boş olamaz.";
            return;
        }

        try
        {
            await PerformLoginAsync(email, password);
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = $"Login hatası: {ex.Message}";
        }
    }

    private async void RegisterButton_Click(object? sender, RoutedEventArgs e)
    {
        RegisterButton.IsEnabled = false;
        RegisterStatusTextBlock.Text = "Kullanıcı kaydı hazırlanıyor...";

        try
        {
            var email = RegisterEmailTextBox.Text?.Trim() ?? string.Empty;
            var password = RegisterPasswordTextBox.Text?.Trim() ?? string.Empty;
            var phoneNumber = RegisterPhoneNumberTextBox.Text?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(phoneNumber))
            {
                RegisterStatusTextBlock.Text = "Email, şifre ve telefon numarası zorunlu.";
                return;
            }

            if (await _databaseService.UserExistsAsync(email))
            {
                RegisterStatusTextBlock.Text = "Bu email zaten kayıtlı.";
                return;
            }

            var sessionStatePath = BuildSessionStatePath(email);
            await _databaseService.SaveOrUpdateUserAsync(email, password, phoneNumber, sessionStatePath);

            LoginEmailTextBox.Text = email;
            LoginPasswordTextBox.Text = password;
            StatusTextBlock.Text = "Kayıt oluşturuldu. Giriş başlatılıyor...";

            ShowLoginView();
            await PerformLoginAsync(email, password);
        }
        catch (Exception ex)
        {
            RegisterStatusTextBlock.Text = $"Kayıt hatası: {ex.Message}";
        }
        finally
        {
            RegisterButton.IsEnabled = true;
        }
    }

    private async Task PerformLoginAsync(string email, string password)
    {
        LoginButton.IsEnabled = false;
        try
        {
            StatusTextBlock.Text = "Kullanıcı bilgileri kontrol ediliyor...";
            var user = await _databaseService.GetUserByCredentialsAsync(email, password);
            if (user is null)
            {
                StatusTextBlock.Text = "Kullanıcı bulunamadı veya şifre hatalı.";
                return;
            }

            var sessionStatePath = BuildSessionStatePath(email);
            if (File.Exists(sessionStatePath))
            {
                _activeUser = new AppUser
                {
                    Id = user.Id,
                    Email = email,
                    Password = password,
                    PhoneNumber = user.PhoneNumber,
                    SessionStatePath = sessionStatePath
                };

                StatusTextBlock.Text = "Kayıtlı oturum bulundu.";
                ShowMainView();
                await LoadHistoryAsync();
                return;
            }

            StatusTextBlock.Text = "Tarayıcı başlatılıyor...";

            _browserAutomationService = new BrowserAutomationService(sessionStatePath);
            _browserAutomationService.ProgressChanged -= BrowserAutomationService_LoginProgressChanged;
            _browserAutomationService.ProgressChanged += BrowserAutomationService_LoginProgressChanged;
            await _browserAutomationService.InitializeAsync(headless: false, restoreSession: false);

            StatusTextBlock.Text = "Yolcu360 giriş ekranı dolduruluyor...";
            await _browserAutomationService.LoginWithPhoneAsync(user.PhoneNumber);

            StatusTextBlock.Text = "SMS doğrulama ekranı bekleniyor...";
            var smsVerificationDetected = false;
            for (var attempt = 0; attempt < 15; attempt++)
            {
                if (await _browserAutomationService.IsSmsVerificationRequiredAsync())
                {
                    smsVerificationDetected = true;
                    break;
                }

                await Task.Delay(1_000);
            }

            if (smsVerificationDetected)
            {
                StatusTextBlock.Text = "SMS doğrulama bekleniyor...";
                var code = await _smsReceiverService.WaitForCodeAsync(TimeSpan.FromMinutes(2));
                await _browserAutomationService.FillSmsVerificationCodeAsync(code);
                await Task.Delay(3_000);
            }

            await _browserAutomationService.SaveCurrentSessionAsync();
            await _databaseService.SaveOrUpdateUserAsync(email, password, user.PhoneNumber, sessionStatePath);

            _activeUser = new AppUser
            {
                Id = user.Id,
                Email = email,
                Password = password,
                PhoneNumber = user.PhoneNumber,
                SessionStatePath = sessionStatePath
            };

            StatusTextBlock.Text = "Giriş tamamlandı.";
            await CloseBrowserAfterAuthAsync();
            ShowMainView();
            await LoadHistoryAsync();
        }
        finally
        {
            LoginButton.IsEnabled = true;
        }
    }

    private void GoToRegisterButton_Click(object? sender, RoutedEventArgs e) => ShowRegisterView();

    private void BackToLoginButton_Click(object? sender, RoutedEventArgs e) => ShowLoginView();

    private void ShowRegisterView()
    {
        LoginView.IsVisible = false;
        RegisterView.IsVisible = true;
        RegisterStatusTextBlock.Text = string.Empty;
    }

    private void ShowLoginView()
    {
        RegisterView.IsVisible = false;
        LoginView.IsVisible = true;
    }

    private static string BuildSessionStatePath(string email)
    {
        var safeFileName = string.Concat(email.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_'));
        const string sessionsDirectory = "/Users/erayoz/Codes/Staj/Yolcu360Otomasyon/sessions";
        return Path.Combine(sessionsDirectory, $"{safeFileName}.json");
    }

    private async Task CloseBrowserAfterAuthAsync()
    {
        if (_browserAutomationService is null)
            return;

        await _browserAutomationService.DisposeAsync();
        _browserAutomationService = null;
    }

    private void ShowMainView()
    {
        LoginView.IsVisible = false;
        RegisterView.IsVisible = false;
        MainView.IsVisible = true;
        ShowSearchSection();
    }

    private async void SearchButton_Click(object? sender, RoutedEventArgs e)
    {
        SearchButton.IsEnabled = false;
        SearchStatusTextBlock.Text = "Arama hazırlanıyor...";

        try
        {
            if (!DateTime.TryParseExact(
                    PickupDateTextBox.Text?.Trim(),
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var pickupDate)
                || !DateTime.TryParseExact(
                    ReturnDateTextBox.Text?.Trim(),
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var returnDate))
            {
                SearchStatusTextBlock.Text = "Tarih formatı gecersiz. Ornek: 2026-08-10";
                return;
            }

            var pickupTime = PickupTimeTextBox.Text?.Trim() ?? "10:00";
            var returnTime = ReturnTimeTextBox.Text?.Trim() ?? "18:00";

            var filter = new SearchFilter
            {
                PickupLocation = PickupLocationTextBox.Text?.Trim() ?? string.Empty,
                PickupDate = pickupDate.Date,
                ReturnDate = returnDate.Date,
                PickupTime = pickupTime,
                ReturnTime = returnTime,
                TransmissionType = GetComboBoxTag(TransmissionComboBox),
                FuelType = GetComboBoxTag(FuelComboBox)
            };

            if (string.IsNullOrWhiteSpace(filter.PickupLocation))
            {
                SearchStatusTextBlock.Text = "Alış yeri boş olamaz.";
                return;
            }

            if (_activeUser is null)
            {
                SearchStatusTextBlock.Text = "Önce giriş yapılmalı.";
                return;
            }

            _browserAutomationService ??= new BrowserAutomationService(_activeUser.SessionStatePath);
            _browserAutomationService.ProgressChanged -= BrowserAutomationService_ProgressChanged;
            _browserAutomationService.ProgressChanged += BrowserAutomationService_ProgressChanged;

            SearchStatusTextBlock.Text = "Tarayıcı başlatılıyor...";
            await _browserAutomationService.InitializeAsync(headless: false, restoreSession: true);

            SearchStatusTextBlock.Text = "Yolcu360 arama formu dolduruluyor...";
            await _browserAutomationService.ApplySearchFiltersAndSearchAsync(filter);

            var results = await _browserAutomationService.ReadSearchResultsAsync();
            _latestResults = results.ToList();
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ResultsDataGrid.ItemsSource = null;
                ResultsDataGrid.ItemsSource = _latestResults;
            });

            SearchStatusTextBlock.Text = _latestResults.Count == 0
                ? "Arama tamamlandı, sonuç bulunamadı."
                : $"{_latestResults.Count} sonuç listelendi. İlk sonuç: {_latestResults[0].Title} | {_latestResults[0].Price}";
        }
        catch (Exception ex)
        {
            // Selector veya bağlantı hataları burada kullanıcıya kısa gösterilir.
            SearchStatusTextBlock.Text = $"Arama hatası: {ex.Message}";
        }
        finally
        {
            SearchButton.IsEnabled = true;
        }
    }

    private static string GetComboBoxTag(ComboBox comboBox)
    {
        return (comboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? string.Empty;
    }

    private void BrowserAutomationService_ProgressChanged(string message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            SearchStatusTextBlock.Text = message;
        });
    }

    private void BrowserAutomationService_LoginProgressChanged(string message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            StatusTextBlock.Text = message;
        });
    }

    private void SmsReceiverService_SmsReceived(string message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            StatusTextBlock.Text = $"SMS alındı: {message}";
        });
    }

    private async void SaveResultsButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_activeUser is null)
        {
            SearchStatusTextBlock.Text = "Önce giriş yapılmalı.";
            return;
        }

        if (_activeUser.Id <= 0)
        {
            var latestUser = await _databaseService.GetUserByEmailAsync(_activeUser.Email);
            if (latestUser is null)
            {
                SearchStatusTextBlock.Text = "Aktif kullanıcı veritabanında bulunamadı.";
                return;
            }

            _activeUser = latestUser;
        }

        if (_latestResults.Count == 0)
        {
            SearchStatusTextBlock.Text = "Kaydedilecek sonuç yok.";
            return;
        }

        var ozelAd = CollectionNameTextBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(ozelAd))
        {
            SearchStatusTextBlock.Text = "Özel kayıt adı girin.";
            return;
        }

        SaveResultsButton.IsEnabled = false;
        try
        {
            var collectionId = await _databaseService.SaveCollectionAsync(_activeUser.Id, ozelAd, _latestResults);
            CollectionNameTextBox.Text = string.Empty;
            SearchStatusTextBlock.Text = $"{_latestResults.Count} sonuç \"{ozelAd}\" adıyla kaydedildi.";
            await LoadHistoryAsync();
            ShowHistorySection();

            var collections = (CollectionsDataGrid.ItemsSource as IEnumerable<KoleksiyonListItem>)?.ToList() ?? new List<KoleksiyonListItem>();
            var savedCollection = collections.FirstOrDefault(item => item.Id == collectionId);
            if (savedCollection is not null)
            {
                CollectionsDataGrid.SelectedItem = savedCollection;
            }
        }
        catch (Exception ex)
        {
            SearchStatusTextBlock.Text = $"Kaydetme hatası: {ex.Message}";
        }
        finally
        {
            SaveResultsButton.IsEnabled = true;
        }
    }

    private async void HistoryTabButton_Click(object? sender, RoutedEventArgs e)
    {
        ShowHistorySection();
        await LoadHistoryAsync();
    }

    private void SearchTabButton_Click(object? sender, RoutedEventArgs e)
    {
        ShowSearchSection();
    }

    private async void CollectionsDataGrid_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (CollectionsDataGrid.SelectedItem is not KoleksiyonListItem selected)
        {
            _selectedCollection = null;
            _selectedCollectionVehicles = new List<SearchResultItem>();
            HistoryVehiclesDataGrid.ItemsSource = null;
            return;
        }

        _selectedCollection = selected;
        _selectedCollectionVehicles = await _databaseService.GetCollectionVehiclesAsync(selected.Id);
        HistoryVehiclesDataGrid.ItemsSource = null;
        HistoryVehiclesDataGrid.ItemsSource = _selectedCollectionVehicles;
        HistoryStatusTextBlock.Text = $"{selected.OzelAd} için {_selectedCollectionVehicles.Count} araç yüklendi.";
    }

    private async void DeleteCollectionButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_activeUser is null || _selectedCollection is null)
        {
            HistoryStatusTextBlock.Text = "Silmek için bir kayıt seçin.";
            return;
        }

        DeleteCollectionButton.IsEnabled = false;
        try
        {
            await _databaseService.DeleteCollectionAsync(_selectedCollection.Id, _activeUser.Id);
            _selectedCollection = null;
            _selectedCollectionVehicles = new List<SearchResultItem>();
            HistoryVehiclesDataGrid.ItemsSource = null;
            await LoadHistoryAsync();
            HistoryStatusTextBlock.Text = "Kayıt silindi.";
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
        if (_selectedCollection is null || _selectedCollectionVehicles.Count == 0)
        {
            HistoryStatusTextBlock.Text = "PNG indirmek için bir kayıt seçin.";
            return;
        }

        ExportPngButton.IsEnabled = false;
        try
        {
            var filePath = await ExportHistorySelectionAsPngAsync(_selectedCollection);
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

        var collections = await _databaseService.GetCollectionsAsync(_activeUser.Id);
        CollectionsDataGrid.ItemsSource = null;
        CollectionsDataGrid.ItemsSource = collections;

        if (collections.Count == 0)
        {
            _selectedCollection = null;
            _selectedCollectionVehicles = new List<SearchResultItem>();
            HistoryVehiclesDataGrid.ItemsSource = null;
            HistoryStatusTextBlock.Text = "Kayıt bulunamadı.";
            return;
        }

        HistoryStatusTextBlock.Text = $"{collections.Count} kayıt listelendi.";
    }

    private void ShowSearchSection()
    {
        SearchPanel.IsVisible = true;
        SearchResultsPanel.IsVisible = true;
        HistoryPanel.IsVisible = false;
        SearchTabButton.Classes.Set("primary", true);
        HistoryTabButton.Classes.Set("primary", false);
    }

    private void ShowHistorySection()
    {
        SearchPanel.IsVisible = false;
        SearchResultsPanel.IsVisible = false;
        HistoryPanel.IsVisible = true;
        SearchTabButton.Classes.Set("primary", false);
        HistoryTabButton.Classes.Set("primary", true);
    }

    private void ConfigureResultsGrid()
    {
        ResultsDataGrid.AutoGenerateColumns = false;
        ResultsDataGrid.Columns.Clear();

        ResultsDataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Araç",
            Binding = new Binding(nameof(SearchResultItem.Title)),
            Width = new DataGridLength(2, DataGridLengthUnitType.Star)
        });

        ResultsDataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Detay",
            Binding = new Binding(nameof(SearchResultItem.Subtitle)),
            Width = new DataGridLength(2, DataGridLengthUnitType.Star)
        });

        ResultsDataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Toplam Fiyat",
            Binding = new Binding(nameof(SearchResultItem.Price)),
            Width = new DataGridLength(1, DataGridLengthUnitType.Star)
        });

        ResultsDataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Günlük",
            Binding = new Binding(nameof(SearchResultItem.DailyPrice)),
            Width = new DataGridLength(1, DataGridLengthUnitType.Star)
        });

        ResultsDataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Vites",
            Binding = new Binding(nameof(SearchResultItem.Transmission)),
            Width = new DataGridLength(1, DataGridLengthUnitType.Star)
        });

        ResultsDataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Yakıt",
            Binding = new Binding(nameof(SearchResultItem.FuelType)),
            Width = new DataGridLength(1, DataGridLengthUnitType.Star)
        });

        ResultsDataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Şirket",
            Binding = new Binding(nameof(SearchResultItem.Supplier)),
            Width = new DataGridLength(1, DataGridLengthUnitType.Star)
        });

        ResultsDataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Teslim",
            Binding = new Binding(nameof(SearchResultItem.PickupInfo)),
            Width = new DataGridLength(2, DataGridLengthUnitType.Star)
        });
    }

    private void ConfigureCollectionsGrid()
    {
        CollectionsDataGrid.AutoGenerateColumns = false;
        CollectionsDataGrid.Columns.Clear();

        CollectionsDataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Kayıt Adı",
            Binding = new Binding(nameof(KoleksiyonListItem.OzelAd)),
            Width = new DataGridLength(2, DataGridLengthUnitType.Star)
        });

        CollectionsDataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Araç Sayısı",
            Binding = new Binding(nameof(KoleksiyonListItem.AracSayisi)),
            Width = new DataGridLength(1, DataGridLengthUnitType.Star)
        });

        CollectionsDataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Tarih",
            Binding = new Binding(nameof(KoleksiyonListItem.OlusturmaTarihi))
            {
                StringFormat = "dd.MM.yyyy HH:mm"
            },
            Width = new DataGridLength(1.4, DataGridLengthUnitType.Star)
        });
    }

    private void ConfigureHistoryVehiclesGrid()
    {
        HistoryVehiclesDataGrid.AutoGenerateColumns = false;
        HistoryVehiclesDataGrid.Columns.Clear();

        HistoryVehiclesDataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Araç",
            Binding = new Binding(nameof(SearchResultItem.Title)),
            Width = new DataGridLength(2, DataGridLengthUnitType.Star)
        });

        HistoryVehiclesDataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Şirket",
            Binding = new Binding(nameof(SearchResultItem.Supplier)),
            Width = new DataGridLength(1.2, DataGridLengthUnitType.Star)
        });

        HistoryVehiclesDataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Vites",
            Binding = new Binding(nameof(SearchResultItem.Transmission)),
            Width = new DataGridLength(1, DataGridLengthUnitType.Star)
        });

        HistoryVehiclesDataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Yakıt",
            Binding = new Binding(nameof(SearchResultItem.FuelType)),
            Width = new DataGridLength(1, DataGridLengthUnitType.Star)
        });

        HistoryVehiclesDataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Toplam Fiyat",
            Binding = new Binding(nameof(SearchResultItem.Price)),
            Width = new DataGridLength(1, DataGridLengthUnitType.Star)
        });
    }

    private async Task<string> ExportHistorySelectionAsPngAsync(KoleksiyonListItem koleksiyon)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            HistoryStatusTextBlock.Text = $"{koleksiyon.OzelAd} PNG olarak hazırlanıyor...";
        });

        await Task.Delay(150);
        await Dispatcher.UIThread.InvokeAsync(() => HistoryExportRoot.InvalidateVisual());
        await Task.Delay(150);

        var downloadsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads");

        Directory.CreateDirectory(downloadsDirectory);

        var safeName = string.Concat(koleksiyon.OzelAd.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_'));
        var filePath = Path.Combine(downloadsDirectory, $"{safeName}_{DateTime.Now:yyyyMMdd_HHmmss}.png");

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var bounds = HistoryExportRoot.Bounds;
            var width = Math.Max(1, (int)Math.Ceiling(bounds.Width));
            var height = Math.Max(1, (int)Math.Ceiling(bounds.Height));
            using var bitmap = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
            bitmap.Render(HistoryExportRoot);

            using var stream = File.Create(filePath);
            bitmap.Save(stream, PngBitmapEncoderOptions.Default);
        });

        return filePath;
    }

    protected override async void OnClosed(EventArgs e)
    {
        if (_browserAutomationService is not null)
            await _browserAutomationService.DisposeAsync();

        await _smsReceiverService.DisposeAsync();

        base.OnClosed(e);
    }
}

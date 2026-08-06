using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Controls.Selection;
using Avalonia.Layout;
using Avalonia.Media;
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
    private List<KoleksiyonListItem> _selectedCollections = new();
    private SearchFilter? _latestSearchFilter;

    public MainWindow()
    {
        InitializeComponent();
        PickupDateTextBox.Text = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        ReturnDateTextBox.Text = DateTime.Today.AddDays(2).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        PickupTimeTextBox.Text = "10:00";
        ReturnTimeTextBox.Text = "18:00";
        ConfigureResultsGrid();
        ConfigureCollectionsGrid();
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
            _latestSearchFilter = filter;

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

        if (_latestSearchFilter is null)
        {
            SearchStatusTextBlock.Text = "Önce geçerli bir arama yapılmalı.";
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
            var collectionId = await _databaseService.SaveCollectionAsync(_activeUser.Id, ozelAd, _latestSearchFilter, _latestResults);
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
        _selectedCollections = CollectionsDataGrid.SelectedItems?.OfType<KoleksiyonListItem>().ToList()
            ?? (CollectionsDataGrid.SelectedItem is KoleksiyonListItem single ? [single] : new List<KoleksiyonListItem>());

        if (_selectedCollections.Count == 0)
        {
            _selectedCollection = null;
            _selectedCollectionVehicles = new List<SearchResultItem>();
            ClearSelectedCollectionSummary();
            return;
        }

        _selectedCollection = _selectedCollections[0];
        _selectedCollectionVehicles = new List<SearchResultItem>();
        UpdateSelectedCollectionSummary(_selectedCollections);
        HistoryStatusTextBlock.Text = _selectedCollections.Count == 1
            ? $"{_selectedCollections[0].OzelAd} kaydı seçildi."
            : $"{_selectedCollections.Count} kayıt seçildi.";
    }

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
            _selectedCollections = new List<KoleksiyonListItem>();
            _selectedCollectionVehicles = new List<SearchResultItem>();
            ClearSelectedCollectionSummary();
            HistoryStatusTextBlock.Text = "Kayıt bulunamadı.";
            return;
        }

        if (_selectedCollection is null || collections.All(item => item.Id != _selectedCollection.Id))
        {
            CollectionsDataGrid.SelectedItem = collections[0];
        }

        HistoryStatusTextBlock.Text = $"{collections.Count} kayıt listelendi.";
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

            var transmission = string.IsNullOrWhiteSpace(collection.SecilenVitesFiltresi)
                ? "Farketmez"
                : collection.SecilenVitesFiltresi;
            var fuel = string.IsNullOrWhiteSpace(collection.SecilenYakitFiltresi)
                ? "Farketmez"
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
            $"Vites: {string.Join(", ", collections.Select(item => string.IsNullOrWhiteSpace(item.SecilenVitesFiltresi) ? "Farketmez" : item.SecilenVitesFiltresi).Distinct())} | " +
            $"Yakıt: {string.Join(", ", collections.Select(item => string.IsNullOrWhiteSpace(item.SecilenYakitFiltresi) ? "Farketmez" : item.SecilenYakitFiltresi).Distinct())}";
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
            Width = new DataGridLength(1.8, DataGridLengthUnitType.Star)
        });

        CollectionsDataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Alış Yeri",
            Binding = new Binding(nameof(KoleksiyonListItem.AlisYeri)),
            Width = new DataGridLength(1.4, DataGridLengthUnitType.Star)
        });

        CollectionsDataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Tarih Aralığı",
            Binding = new Binding(nameof(KoleksiyonListItem.AlisTarihi))
            {
                StringFormat = "dd.MM.yyyy"
            },
            Width = new DataGridLength(1.1, DataGridLengthUnitType.Star)
        });

        CollectionsDataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Dönüş",
            Binding = new Binding(nameof(KoleksiyonListItem.DonusTarihi))
            {
                StringFormat = "dd.MM.yyyy"
            },
            Width = new DataGridLength(1.1, DataGridLengthUnitType.Star)
        });

        CollectionsDataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Vites",
            Binding = new Binding(nameof(KoleksiyonListItem.SecilenVitesFiltresi)),
            Width = new DataGridLength(1, DataGridLengthUnitType.Star)
        });

        CollectionsDataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Yakıt",
            Binding = new Binding(nameof(KoleksiyonListItem.SecilenYakitFiltresi)),
            Width = new DataGridLength(1, DataGridLengthUnitType.Star)
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

    private async Task<string> ExportHistorySelectionAsPngAsync(IReadOnlyList<KoleksiyonListItem> collections)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            HistoryStatusTextBlock.Text = collections.Count == 1
                ? $"{collections[0].OzelAd} PNG olarak hazırlanıyor..."
                : $"{collections.Count} kayıt için PNG hazırlanıyor...";
        });

        var downloadsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads");

        Directory.CreateDirectory(downloadsDirectory);

        var baseName = collections.Count == 1 ? collections[0].OzelAd : $"{collections.Count}_kayit";
        var safeName = string.Concat(baseName.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_'));
        var filePath = Path.Combine(downloadsDirectory, $"{safeName}_{DateTime.Now:yyyyMMdd_HHmmss}.png");

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            const double reportWidth = 1440;
            var report = BuildCollectionReportVisual(collections);
            report.Measure(new Size(reportWidth, double.PositiveInfinity));
            report.Arrange(new Rect(0, 0, reportWidth, report.DesiredSize.Height));

            var width = Math.Max(1, (int)Math.Ceiling(report.Bounds.Width));
            var height = Math.Max(1, (int)Math.Ceiling(report.Bounds.Height));

            using var bitmap = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
            bitmap.Render(report);

            using var stream = File.Create(filePath);
            bitmap.Save(stream, PngBitmapEncoderOptions.Default);
        });

        return filePath;
    }

    private static Control BuildCollectionReportVisual(IReadOnlyList<KoleksiyonListItem> collections)
    {
        var root = new Border
        {
            Width = 1440,
            Background = new SolidColorBrush(Color.Parse("#F4F7FB")),
            Padding = new Thickness(28)
        };

        var container = new StackPanel
        {
            Spacing = 18
        };

        container.Children.Add(new Border
        {
            Background = new SolidColorBrush(Color.Parse("#0F172A")),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(24),
            Child = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock
                    {
                        Text = collections.Count == 1 ? collections[0].OzelAd : $"{collections.Count} Seçili Kayıt",
                        FontSize = 30,
                        FontWeight = FontWeight.Bold,
                        Foreground = Brushes.White
                    },
                    new TextBlock
                    {
                        Text = $"Alış Yeri: {string.Join(", ", collections.Select(item => item.AlisYeri).Distinct())}",
                        Foreground = new SolidColorBrush(Color.Parse("#D6E2F0"))
                    },
                    new TextBlock
                    {
                        Text = collections.Count == 1
                            ? $"Tarih: {collections[0].AlisTarihi:dd.MM.yyyy} {collections[0].AlisSaati} - {collections[0].DonusTarihi:dd.MM.yyyy} {collections[0].DonusSaati}"
                            : $"Tarih Aralığı: {collections.Min(item => item.AlisTarihi):dd.MM.yyyy} - {collections.Max(item => item.DonusTarihi):dd.MM.yyyy}",
                        Foreground = new SolidColorBrush(Color.Parse("#D6E2F0"))
                    },
                    new TextBlock
                    {
                        Text =
                            $"Filtreler: Vites = {string.Join(", ", collections.Select(item => FormatFilterValue(item.SecilenVitesFiltresi)).Distinct())}, " +
                            $"Yakıt = {string.Join(", ", collections.Select(item => FormatFilterValue(item.SecilenYakitFiltresi)).Distinct())}",
                        Foreground = new SolidColorBrush(Color.Parse("#D6E2F0"))
                    },
                    new TextBlock
                    {
                        Text = $"Araç Sayısı: {collections.Sum(item => item.AracSayisi)} | Oluşturulma: {collections.Min(item => item.OlusturmaTarihi).ToLocalTime():dd.MM.yyyy HH:mm}",
                        Foreground = new SolidColorBrush(Color.Parse("#D6E2F0"))
                    }
                }
            }
        });

        foreach (var collection in collections)
            container.Children.Add(BuildSingleCollectionSummaryCard(collection));

        root.Child = container;
        return root;
    }

    private static Control BuildSingleCollectionSummaryCard(KoleksiyonListItem collection)
    {
        return new Border
        {
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#D9E2EC")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(22),
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,*"),
                RowDefinitions = new RowDefinitions("Auto,Auto,Auto"),
                ColumnSpacing = 18,
                RowSpacing = 14,
                Children =
                {
                    CreateSummaryBlock("Kayıt Adı", collection.OzelAd, 0, 0),
                    CreateSummaryBlock("Alış Yeri", collection.AlisYeri, 0, 1),
                    CreateSummaryBlock(
                        "Tarih Aralığı",
                        $"{collection.AlisTarihi:dd.MM.yyyy} {collection.AlisSaati} - {collection.DonusTarihi:dd.MM.yyyy} {collection.DonusSaati}",
                        1,
                        0),
                    CreateSummaryBlock(
                        "Filtreler",
                        $"Vites: {FormatFilterValue(collection.SecilenVitesFiltresi)} | Yakıt: {FormatFilterValue(collection.SecilenYakitFiltresi)}",
                        1,
                        1),
                    CreateSummaryBlock("Araç Sayısı", collection.AracSayisi.ToString(), 2, 0),
                    CreateSummaryBlock("Oluşturulma", collection.OlusturmaTarihi.ToLocalTime().ToString("dd.MM.yyyy HH:mm"), 2, 1)
                }
            }
        };
    }

    private static Control CreateSummaryBlock(string title, string value, int row, int column)
    {
        var panel = new StackPanel
        {
            Spacing = 4,
            Children =
            {
                new TextBlock
                {
                    Text = title,
                    FontWeight = FontWeight.Bold,
                    Foreground = new SolidColorBrush(Color.Parse("#122033"))
                },
                new TextBlock
                {
                    Text = string.IsNullOrWhiteSpace(value) ? "-" : value,
                    Foreground = new SolidColorBrush(Color.Parse("#132235")),
                    TextWrapping = TextWrapping.Wrap
                }
            }
        };

        Grid.SetRow(panel, row);
        Grid.SetColumn(panel, column);
        return panel;
    }

    private static string FormatFilterValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "Farketmez" : value;
    }

    protected override async void OnClosed(EventArgs e)
    {
        if (_browserAutomationService is not null)
            await _browserAutomationService.DisposeAsync();

        await _smsReceiverService.DisposeAsync();

        base.OnClosed(e);
    }
}

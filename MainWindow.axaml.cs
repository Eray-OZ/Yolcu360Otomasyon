using System.Globalization;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Yolcu360Otomasyon.Models;
using Yolcu360Otomasyon.Configuration;
using Yolcu360Otomasyon.Services;

namespace Yolcu360Otomasyon;

public partial class MainWindow : Window
{
    private BrowserAutomationService? _browserAutomationService;
    private readonly ObservableCollection<SearchResultItem> _searchResults = [];

    public MainWindow()
    {
        InitializeComponent();
        ResultsDataGrid.ItemsSource = _searchResults;
    }

    private async void SaveLoginInfoButton_Click(object? sender, RoutedEventArgs e)
    {
        SaveLoginInfoButton.IsEnabled = false;
        StatusTextBlock.Text = "Login bilgileri kaydediliyor...";

        try
        {
            var email = EmailTextBox.Text?.Trim() ?? string.Empty;
            var password = PasswordTextBox.Text ?? string.Empty;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                StatusTextBlock.Text = "E-posta ve şifre boş olamaz.";
                return;
            }

            var connectionString = AppSettings.GetConnectionString();
            var database = new DatabaseService(connectionString);

            await database.SaveLoginUserAsync(email, password);

            StatusTextBlock.Text = "Login bilgileri veritabanına kaydedildi.";
            PasswordTextBox.Text = string.Empty;
        }
        catch (Exception ex)
        {
            // Veritabanı veya secrets hatasını ekranda kısa gösterir.
            StatusTextBlock.Text = $"Kayıt hatası: {ex.Message}";
        }
        finally
        {
            SaveLoginInfoButton.IsEnabled = true;
        }
    }

    private async void LoginButton_Click(object? sender, RoutedEventArgs e)
    {
        LoginButton.IsEnabled = false;
        StatusTextBlock.Text = "Veritabanından kullanıcı okunuyor...";

        try
        {
            var connectionString = AppSettings.GetConnectionString();
            var database = new DatabaseService(connectionString);
            var user = await database.GetDefaultUserAsync();

            if (user is null)
            {
                StatusTextBlock.Text = "users tablosunda kayıtlı kullanıcı yok.";
                return;
            }

            StatusTextBlock.Text = "Tarayıcı başlatılıyor...";

            _browserAutomationService = new BrowserAutomationService();
            await _browserAutomationService.InitializeAsync(headless: true);

            StatusTextBlock.Text = "Yolcu360 giriş işlemi yapılıyor...";
            await _browserAutomationService.LoginAsync(user);

            StatusTextBlock.Text = "Giriş tamamlandı.";
        }
        catch (Exception ex)
        {
            // Hata mesajını kısa tutup kullanıcıya anlaşılır şekilde gösterir.
            StatusTextBlock.Text = $"Login hatası: {ex.Message}";
        }
        finally
        {
            LoginButton.IsEnabled = true;
        }
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
                SearchStatusTextBlock.Text = "Tarih formatı geçersiz. Örnek: 2026-08-10";
                return;
            }

            var filter = new SearchFilter
            {
                PickupLocation = PickupLocationTextBox.Text?.Trim() ?? string.Empty,
                PickupDate = pickupDate,
                ReturnDate = returnDate,
                PickupTime = PickupTimeTextBox.Text?.Trim() ?? "10:00",
                ReturnTime = ReturnTimeTextBox.Text?.Trim() ?? "18:00",
                TransmissionType = GetComboBoxTag(TransmissionComboBox),
                FuelType = GetComboBoxTag(FuelComboBox)
            };

            if (string.IsNullOrWhiteSpace(filter.PickupLocation))
            {
                SearchStatusTextBlock.Text = "Alış yeri boş olamaz.";
                return;
            }

            _browserAutomationService ??= new BrowserAutomationService();
            _browserAutomationService.ProgressChanged -= BrowserAutomationService_ProgressChanged;
            _browserAutomationService.ProgressChanged += BrowserAutomationService_ProgressChanged;

            SearchStatusTextBlock.Text = "Tarayıcı başlatılıyor...";
            await _browserAutomationService.InitializeAsync(headless: false);

            SearchStatusTextBlock.Text = "Yolcu360 arama formu dolduruluyor...";
            await _browserAutomationService.ApplySearchFiltersAndSearchAsync(filter);

            var results = await _browserAutomationService.ReadSearchResultsAsync();

            _searchResults.Clear();
            foreach (var result in results)
                _searchResults.Add(result);

            SearchStatusTextBlock.Text = results.Count == 0
                ? "Arama tamamlandı, sonuç bulunamadı."
                : $"{results.Count} sonuç listelendi.";
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

    protected override async void OnClosed(EventArgs e)
    {
        if (_browserAutomationService is not null)
            await _browserAutomationService.DisposeAsync();

        base.OnClosed(e);
    }
}

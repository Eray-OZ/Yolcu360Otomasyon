using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Yolcu360Otomasyon.Models;
using Yolcu360Otomasyon.Services;

namespace Yolcu360Otomasyon;

public partial class MainWindow : Window
{
    private BrowserAutomationService? _browserAutomationService;
    private readonly SmsReceiverService _smsReceiverService = new();

    public MainWindow()
    {
        InitializeComponent();
        PickupDateTextBox.Text = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        ReturnDateTextBox.Text = DateTime.Today.AddDays(2).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        PickupTimeTextBox.Text = "10:00";
        ReturnTimeTextBox.Text = "18:00";
        ConfigureResultsGrid();
        _smsReceiverService.SmsReceived += SmsReceiverService_SmsReceived;
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
        LoginButton.IsEnabled = false;
        StatusTextBlock.Text = "Telefon numarası kontrol ediliyor...";

        try
        {
            var phoneNumber = PhoneNumberTextBox.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(phoneNumber))
            {
                StatusTextBlock.Text = "Telefon numarası boş olamaz.";
                return;
            }

            StatusTextBlock.Text = "Tarayıcı başlatılıyor...";

            _browserAutomationService = new BrowserAutomationService();
            _browserAutomationService.ProgressChanged -= BrowserAutomationService_LoginProgressChanged;
            _browserAutomationService.ProgressChanged += BrowserAutomationService_LoginProgressChanged;
            await _browserAutomationService.InitializeAsync(headless: false, restoreSession: false);

            StatusTextBlock.Text = "Yolcu360 giriş ekranı dolduruluyor...";
            await _browserAutomationService.LoginWithPhoneAsync(phoneNumber);

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
                await _browserAutomationService.SaveCurrentSessionAsync();
                StatusTextBlock.Text = "Giriş tamamlandı, oturum kaydedildi.";
            }
            else
            {
                await _browserAutomationService.SaveCurrentSessionAsync();
                StatusTextBlock.Text = "Giriş durumu kaydedildi.";
            }
        }
        catch (Exception ex)
        {
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

            _browserAutomationService ??= new BrowserAutomationService();
            _browserAutomationService.ProgressChanged -= BrowserAutomationService_ProgressChanged;
            _browserAutomationService.ProgressChanged += BrowserAutomationService_ProgressChanged;

            SearchStatusTextBlock.Text = "Tarayıcı başlatılıyor...";
            await _browserAutomationService.InitializeAsync(headless: false);

            SearchStatusTextBlock.Text = "Yolcu360 arama formu dolduruluyor...";
            await _browserAutomationService.ApplySearchFiltersAndSearchAsync(filter);

            var results = await _browserAutomationService.ReadSearchResultsAsync();
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ResultsDataGrid.ItemsSource = null;
                ResultsDataGrid.ItemsSource = results.ToList();
            });

            SearchStatusTextBlock.Text = results.Count == 0
                ? "Arama tamamlandı, sonuç bulunamadı."
                : $"{results.Count} sonuç listelendi. İlk sonuç: {results[0].Title} | {results[0].Price}";
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

    protected override async void OnClosed(EventArgs e)
    {
        if (_browserAutomationService is not null)
            await _browserAutomationService.DisposeAsync();

        await _smsReceiverService.DisposeAsync();

        base.OnClosed(e);
    }
}

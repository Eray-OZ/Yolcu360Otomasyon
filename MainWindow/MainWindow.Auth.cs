using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Yolcu360Otomasyon.Models;
using Yolcu360Otomasyon.Services;

namespace Yolcu360Otomasyon;

public partial class MainWindow : Window
{
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
}

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
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
            SetAuthStatus(_smsReceiverService.GetStatusMessage());
        }
        catch (Exception ex)
        {
            SetAuthStatus($"SMS alıcısı başlatılamadı: {ex.Message}");
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
            if (File.Exists(sessionStatePath))
            {
                try { File.Delete(sessionStatePath); } catch { }
            }

            await _databaseService.SaveOrUpdateUserAsync(email, password, phoneNumber, sessionStatePath);

            LoginEmailTextBox.Text = email;
            LoginPasswordTextBox.Text = password;
            StatusTextBlock.Text = "Kayıt oluşturuldu. Gömülü tarayıcıda giriş başlatılıyor...";

            await PerformLoginAsync(email, password, forceBrowserLogin: true);
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

    private async Task PerformLoginAsync(string email, string password, bool forceBrowserLogin = false)
    {
        LoginButton.IsEnabled = false;
        SetNavigationEnabled(false);
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
            if (!forceBrowserLogin && File.Exists(sessionStatePath))
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

            LoginView.IsVisible = false;
            RegisterView.IsVisible = false;
            MainView.IsVisible = true;
            ShowBrowserSection();
            SetNavigationVisibility(false);

            StatusTextBlock.Text = "Gömülü tarayıcı hazırlanıyor...";

            var baService = CreateBAService();
            await baService.ClearBrowserSessionAsync();

            StatusTextBlock.Text = "Yolcu360 giriş ekranı dolduruluyor...";
            _smsReceiverService.ClearLatestCode();
            await baService.LoginWithPhoneAsync(user.PhoneNumber);

            StatusTextBlock.Text = "SMS doğrulama kodu bekleniyor...";
            string code;
            try
            {
                code = await _smsReceiverService.WaitForCodeAsync(TimeSpan.FromMinutes(2));
            }
            catch (OperationCanceledException)
            {
                throw new InvalidOperationException(
                    $"SMS kodu 2 dakika içinde uygulamaya gelmedi. MacroDroid URL'i şu formatta olmalı: http://{SmsReceiverService.GetPreferredLocalIpAddress()}:{_smsReceiverService.Port}/sms?message={{sms_message}}");
            }

            StatusTextBlock.Text = $"SMS kodu alındı: {code}";
            await baService.FillSmsVerificationCodeAsync(code);
            StatusTextBlock.Text = "Girişin tamamlanması bekleniyor...";
            await baService.WaitForLoginCompletedAsync();

            StatusTextBlock.Text = "Oturum kaydediliyor...";
            await baService.SaveSessionAsync(sessionStatePath);
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
            ShowMainView();
            SetNavigationVisibility(true);
            ShowSearchSection();
            await LoadHistoryAsync();
        }
        catch (Exception ex)
        {
            SetNavigationVisibility(true);
            ShowLoginView();
            StatusTextBlock.Text = $"Giriş hatası: {ex.Message}";
        }
        finally
        {
            LoginButton.IsEnabled = true;
            SetNavigationEnabled(true);
        }
    }

    private void LogoutButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_isAuthenticating) return;
        _activeUser = null;
        SetNavigationVisibility(false);
        ShowLoginView();
        StatusTextBlock.Text = "Çıkış yapıldı.";
    }

    private void GoToRegisterButton_Click(object? sender, RoutedEventArgs e) => ShowRegisterView();

    private void BackToLoginButton_Click(object? sender, RoutedEventArgs e) => ShowLoginView();

    private void ShowRegisterView()
    {
        MainView.IsVisible = false;
        SetNavigationVisibility(false);
        HideMainContentPanels();
        LoginView.IsVisible = false;
        RegisterView.IsVisible = true;
        RegisterStatusTextBlock.Text = string.Empty;
    }

    private void ShowLoginView()
    {
        MainView.IsVisible = false;
        SetNavigationVisibility(false);
        HideMainContentPanels();
        RegisterView.IsVisible = false;
        LoginView.IsVisible = true;
    }

    private static string BuildSessionStatePath(string email)
    {
        var safeFileName = string.Concat(email.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_'));
        var sessionsDirectory = Path.Combine(ResolveAppDataDirectory(), "sessions");
        return Path.Combine(sessionsDirectory, $"{safeFileName}.json");
    }

    private static string ResolveAppDataDirectory()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Yolcu360Otomasyon.csproj")))
                return current.FullName;

            current = current.Parent;
        }

        return AppContext.BaseDirectory;
    }

    private void ShowMainView()
    {
        LoginView.IsVisible = false;
        RegisterView.IsVisible = false;
        MainView.IsVisible = true;
        SetNavigationVisibility(true);
        ShowSearchSection();
    }

    private void HideMainContentPanels()
    {
        ResetBrowserPanelVisualState();
        SearchPanel.IsVisible = false;
        SearchResultsPanel.IsVisible = false;
        FlightPanel.IsVisible = false;
        HistoryPanel.IsVisible = false;
        PaymentsPanel.IsVisible = false;
        PaymentCheckoutPanel.IsVisible = false;
        BrowserSectionPanel.IsVisible = false;
        SearchTabButton.Classes.Set("primary", false);
        FlightTabButton.Classes.Set("primary", false);
        HistoryTabButton.Classes.Set("primary", false);
        PaymentsTabButton.Classes.Set("primary", false);
    }

    private void SmsReceiverService_SmsReceived(string message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            SetAuthStatus($"SMS alındı: {message}");
        });
    }

    private void SetAuthStatus(string message)
    {
        StatusTextBlock.Text = message;

        if (RegisterView.IsVisible)
            RegisterStatusTextBlock.Text = message;
    }
}

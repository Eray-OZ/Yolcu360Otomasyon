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
            SetAuthStatus(BuildSmsReceiverStatus());
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

            // Gömülü tarayıcıyı canlı göstermek için Ana Görünüme ve Tarayıcı Paneline geç
            LoginView.IsVisible = false;
            RegisterView.IsVisible = false;
            MainView.IsVisible = true;
            ShowBrowserSection();
            SetNavigationVisibility(false);

            StatusTextBlock.Text = "Gömülü tarayıcı hazırlanıyor...";
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);

            var embeddedBrowser = CreateEmbeddedBrowserAutomationService();
            await embeddedBrowser.ClearBrowserSessionAsync();

            StatusTextBlock.Text = "Yolcu360 giriş ekranı dolduruluyor...";
            _smsReceiverService.ClearLatestCode();
            await embeddedBrowser.LoginWithPhoneAsync(user.PhoneNumber);

            StatusTextBlock.Text = "SMS doğrulama kodu bekleniyor...";
            string code;
            try
            {
                code = await _smsReceiverService.WaitForCodeAsync(TimeSpan.FromMinutes(2));
            }
            catch (OperationCanceledException)
            {
                throw new InvalidOperationException(
                    $"SMS kodu 2 dakika içinde uygulamaya gelmedi. MacroDroid URL'i şu formatta olmalı: http://{GetPreferredLocalIpAddress()}:{_smsReceiverService.Port}/sms?message={{sms_message}}");
            }

            StatusTextBlock.Text = $"SMS kodu alındı: {code}";
            await embeddedBrowser.FillSmsVerificationCodeAsync(code);
            StatusTextBlock.Text = "Girişin tamamlanması bekleniyor...";
            await embeddedBrowser.WaitForLoginCompletedAsync();

            StatusTextBlock.Text = "Oturum kaydediliyor...";
            await embeddedBrowser.SaveSessionAsync(sessionStatePath);
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
        ShowLoginView();
        StatusTextBlock.Text = "Çıkış yapıldı.";
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
        ShowSearchSection();
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

    private string BuildSmsReceiverStatus()
    {
        var addresses = GetLocalIpAddresses().ToArray();
        var primaryAddress = addresses.FirstOrDefault() ?? "127.0.0.1";
        var alternatives = addresses.Length > 1
            ? $" Alternatif IP: {string.Join(", ", addresses.Skip(1))}"
            : string.Empty;

        return $"SMS alıcısı hazır. MacroDroid URL: http://{primaryAddress}:{_smsReceiverService.Port}/sms?message={{sms_message}}{alternatives}";
    }

    private static string GetPreferredLocalIpAddress()
    {
        return GetLocalIpAddresses().FirstOrDefault() ?? "127.0.0.1";
    }

    private static IEnumerable<string> GetLocalIpAddresses()
    {
        var addresses = new List<string>();

        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (networkInterface.OperationalStatus != OperationalStatus.Up)
                continue;

            if (networkInterface.NetworkInterfaceType is not (NetworkInterfaceType.Wireless80211 or NetworkInterfaceType.Ethernet))
                continue;

            var properties = networkInterface.GetIPProperties();
            foreach (var address in properties.UnicastAddresses)
            {
                var ip = address.Address;
                if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                    addresses.Add(ip.ToString());
            }
        }

        if (addresses.Count > 0)
            return addresses.Distinct();

        try
        {
            return Dns.GetHostEntry(Dns.GetHostName())
                .AddressList
                .Where(ip => ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                .Select(ip => ip.ToString())
                .Distinct();
        }
        catch
        {
            return [];
        }
    }
}

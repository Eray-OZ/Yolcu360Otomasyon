using Avalonia.Interactivity;
using Yolcu360Otomasyon.Models;
using Yolcu360Otomasyon.Services;

namespace Yolcu360Otomasyon;

public partial class MainWindow
{
    private async void LoginButton_Click(object? sender, RoutedEventArgs e)
    {
        var email = LoginEmailTextBox.Text?.Trim() ?? string.Empty;
        var password = LoginPasswordTextBox.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            SetAuthStatus("Email ve şifre boş olamaz.");
            return;
        }

        try
        {
            await PerformLoginAsync(email, password);
        }
        catch (Exception ex)
        {
            SetAuthStatus($"Login hatası: {ex.Message}");
        }
    }

    private async Task PerformLoginAsync(string email, string password, bool forceBrowserLogin = false)
    {
        LoginButton.IsEnabled = false;
        SetNavigationEnabled(false);
        try
        {
            SetAuthStatus("Kullanıcı bilgileri kontrol ediliyor...");
            var user = await _databaseService.GetUserByCredentialsAsync(email, password);
            if (user is null)
            {
                SetAuthStatus("Kullanıcı bulunamadı veya şifre hatalı.");
                return;
            }

            var sessionStatePath = AppPaths.BuildSessionStatePath(email);
            if (await TryUseSavedSessionAsync(user, email, password, sessionStatePath, forceBrowserLogin))
                return;

            ShowBrowserLoginView();
            var embeddedBrowser = await RunPhoneLoginAsync(user.PhoneNumber);
            var code = await WaitForSmsCodeAsync();
            await SubmitSmsCodeAndWaitForLoginAsync(embeddedBrowser, code);
            await SaveLoginSessionAsync(embeddedBrowser, user, email, password, sessionStatePath);

            SetAuthStatus("Giriş tamamlandı.");
            ShowMainView();
            SetNavigationVisibility(true);
            ShowSearchSection();
            await LoadHistoryAsync();
        }
        catch (Exception ex)
        {
            SetNavigationVisibility(true);
            ShowLoginView();
            SetAuthStatus($"Giriş hatası: {ex.Message}");
        }
        finally
        {
            LoginButton.IsEnabled = true;
            SetNavigationEnabled(true);
        }
    }

    private async Task<bool> TryUseSavedSessionAsync(
        AppUser user,
        string email,
        string password,
        string sessionStatePath,
        bool forceBrowserLogin)
    {
        if (forceBrowserLogin || !File.Exists(sessionStatePath))
            return false;

        SetActiveUser(user, email, password, sessionStatePath);
        SetAuthStatus("Kayıtlı oturum bulundu.");
        ShowMainView();
        await LoadHistoryAsync();
        return true;
    }

    private async Task<EmbeddedBrowserAutomationService> RunPhoneLoginAsync(string phoneNumber)
    {
        SetAuthStatus("Gömülü tarayıcı hazırlanıyor...");

        var embeddedBrowser = GetEmbeddedBrowserAutomationService();
        await embeddedBrowser.ClearBrowserSessionAsync();

        SetAuthStatus("Yolcu360 giriş ekranı dolduruluyor...");
        _smsReceiverService.ClearLatestCode();
        await embeddedBrowser.LoginWithPhoneAsync(phoneNumber);

        return embeddedBrowser;
    }

    private async Task<string> WaitForSmsCodeAsync()
    {
        SetAuthStatus("SMS doğrulama kodu bekleniyor...");

        try
        {
            return await _smsReceiverService.WaitForCodeAsync(TimeSpan.FromMinutes(2));
        }
        catch (OperationCanceledException)
        {
            throw new InvalidOperationException(
                $"SMS kodu 2 dakika içinde uygulamaya gelmedi. MacroDroid URL'i şu formatta olmalı: http://{SmsReceiverService.GetPreferredLocalIpAddress()}:{_smsReceiverService.Port}/sms?message={{sms_message}}");
        }
    }

    private async Task SubmitSmsCodeAndWaitForLoginAsync(EmbeddedBrowserAutomationService embeddedBrowser, string code)
    {
        SetAuthStatus($"SMS kodu alındı: {code}");
        await embeddedBrowser.FillSmsVerificationCodeAsync(code);
        SetAuthStatus("Girişin tamamlanması bekleniyor...");
        await embeddedBrowser.WaitForLoginCompletedAsync();
    }

    private async Task SaveLoginSessionAsync(
        EmbeddedBrowserAutomationService embeddedBrowser,
        AppUser user,
        string email,
        string password,
        string sessionStatePath)
    {
        SetAuthStatus("Oturum kaydediliyor...");
        await embeddedBrowser.SaveSessionAsync(sessionStatePath);
        await _databaseService.SaveOrUpdateUserAsync(email, password, user.PhoneNumber, sessionStatePath);
        SetActiveUser(user, email, password, sessionStatePath);
    }

    private void SetActiveUser(AppUser user, string email, string password, string sessionStatePath)
    {
        _activeUser = new AppUser
        {
            Id = user.Id,
            Email = email,
            Password = password,
            PhoneNumber = user.PhoneNumber,
            SessionStatePath = sessionStatePath
        };
    }
}

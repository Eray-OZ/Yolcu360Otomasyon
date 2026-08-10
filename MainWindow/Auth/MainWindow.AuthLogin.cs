using Avalonia.Interactivity;

namespace Yolcu360Otomasyon;

public partial class MainWindow
{
    private async void LoginButton_Click(object? sender, RoutedEventArgs e)
    {
        var email = LoginEmailTextBoxControl.Text?.Trim() ?? string.Empty;
        var password = LoginPasswordTextBoxControl.Text?.Trim() ?? string.Empty;

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
        LoginButtonControl.IsEnabled = false;
        SetNavigationEnabled(false);
        try
        {
            var result = await _authWorkflowService.LoginAsync(email, password, forceBrowserLogin);
            if (!result.Success || result.User is null)
            {
                SetAuthStatus(result.ErrorMessage ?? "Giriş tamamlanamadı.");
                return;
            }

            _activeUser = result.User;
            if (!result.UsedSavedSession)
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
            LoginButtonControl.IsEnabled = true;
            SetNavigationEnabled(true);
        }
    }
}

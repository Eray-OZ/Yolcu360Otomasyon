using Avalonia.Interactivity;
using Yolcu360Otomasyon.Services;

namespace Yolcu360Otomasyon;

public partial class MainWindow
{
    private async void RegisterButton_Click(object? sender, RoutedEventArgs e)
    {
        RegisterButtonControl.IsEnabled = false;
        RegisterStatusTextBlockControl.Text = "Kullanıcı kaydı hazırlanıyor...";

        try
        {
            var email = RegisterEmailTextBoxControl.Text?.Trim() ?? string.Empty;
            var password = RegisterPasswordTextBoxControl.Text?.Trim() ?? string.Empty;
            var phoneNumber = RegisterPhoneNumberTextBoxControl.Text?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(phoneNumber))
            {
                RegisterStatusTextBlockControl.Text = "Email, şifre ve telefon numarası zorunlu.";
                return;
            }

            if (await _databaseService.UserExistsAsync(email))
            {
                RegisterStatusTextBlockControl.Text = "Bu email zaten kayıtlı.";
                return;
            }

            var sessionStatePath = AppPaths.BuildSessionStatePath(email);
            if (File.Exists(sessionStatePath))
            {
                try { File.Delete(sessionStatePath); } catch { }
            }

            await _databaseService.SaveOrUpdateUserAsync(email, password, phoneNumber, sessionStatePath);

            LoginEmailTextBoxControl.Text = email;
            LoginPasswordTextBoxControl.Text = password;
            SetAuthStatus("Kayıt oluşturuldu. Gömülü tarayıcıda giriş başlatılıyor...");

            await PerformLoginAsync(email, password, forceBrowserLogin: true);
        }
        catch (Exception ex)
        {
            RegisterStatusTextBlockControl.Text = $"Kayıt hatası: {ex.Message}";
        }
        finally
        {
            RegisterButtonControl.IsEnabled = true;
        }
    }
}

using Avalonia.Interactivity;
using Yolcu360Otomasyon.Services;

namespace Yolcu360Otomasyon;

public partial class MainWindow
{
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

            var sessionStatePath = AppPaths.BuildSessionStatePath(email);
            if (File.Exists(sessionStatePath))
            {
                try { File.Delete(sessionStatePath); } catch { }
            }

            await _databaseService.SaveOrUpdateUserAsync(email, password, phoneNumber, sessionStatePath);

            LoginEmailTextBox.Text = email;
            LoginPasswordTextBox.Text = password;
            SetAuthStatus("Kayıt oluşturuldu. Gömülü tarayıcıda giriş başlatılıyor...");

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
}

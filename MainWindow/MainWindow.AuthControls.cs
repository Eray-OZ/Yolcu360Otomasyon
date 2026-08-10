using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace Yolcu360Otomasyon;

public partial class MainWindow
{
    private Border LoginView => AuthViewControl.FindControl<Border>("LoginView")!;
    private Border RegisterView => AuthViewControl.FindControl<Border>("RegisterView")!;
    private TextBox LoginEmailTextBox => AuthViewControl.FindControl<TextBox>("LoginEmailTextBox")!;
    private TextBox LoginPasswordTextBox => AuthViewControl.FindControl<TextBox>("LoginPasswordTextBox")!;
    private TextBox RegisterEmailTextBox => AuthViewControl.FindControl<TextBox>("RegisterEmailTextBox")!;
    private TextBox RegisterPasswordTextBox => AuthViewControl.FindControl<TextBox>("RegisterPasswordTextBox")!;
    private TextBox RegisterPhoneNumberTextBox => AuthViewControl.FindControl<TextBox>("RegisterPhoneNumberTextBox")!;
    private Button LoginButton => AuthViewControl.FindControl<Button>("LoginButton")!;
    private Button RegisterButton => AuthViewControl.FindControl<Button>("RegisterButton")!;
    private Button GoToRegisterButton => AuthViewControl.FindControl<Button>("GoToRegisterButton")!;
    private Button BackToLoginButton => AuthViewControl.FindControl<Button>("BackToLoginButton")!;
    private TextBlock StatusTextBlock => AuthViewControl.FindControl<TextBlock>("StatusTextBlock")!;
    private TextBlock RegisterStatusTextBlock => AuthViewControl.FindControl<TextBlock>("RegisterStatusTextBlock")!;

    private void ConfigureAuthViewEvents()
    {
        LoginButton.Click += LoginButton_Click;
        RegisterButton.Click += RegisterButton_Click;
        GoToRegisterButton.Click += GoToRegisterButton_Click;
        BackToLoginButton.Click += BackToLoginButton_Click;
    }

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

    private void LogoutButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_isAuthenticating) return;
        _activeUser = null;
        ShowLoginView();
        SetAuthStatus("Çıkış yapıldı.");
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

    private void ShowMainView()
    {
        LoginView.IsVisible = false;
        RegisterView.IsVisible = false;
        MainView.IsVisible = true;
        ShowSearchSection();
    }

    private void ShowBrowserLoginView()
    {
        LoginView.IsVisible = false;
        RegisterView.IsVisible = false;
        MainView.IsVisible = true;
        ShowBrowserSection();
        SetNavigationVisibility(false);
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

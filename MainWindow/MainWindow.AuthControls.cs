using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace Yolcu360Otomasyon;

public partial class MainWindow
{
    private Border LoginViewControl => AuthViewRootControl.FindControl<Border>("LoginView")!;
    private Border RegisterViewControl => AuthViewRootControl.FindControl<Border>("RegisterView")!;
    private TextBox LoginEmailTextBoxControl => AuthViewRootControl.FindControl<TextBox>("LoginEmailTextBox")!;
    private TextBox LoginPasswordTextBoxControl => AuthViewRootControl.FindControl<TextBox>("LoginPasswordTextBox")!;
    private TextBox RegisterEmailTextBoxControl => AuthViewRootControl.FindControl<TextBox>("RegisterEmailTextBox")!;
    private TextBox RegisterPasswordTextBoxControl => AuthViewRootControl.FindControl<TextBox>("RegisterPasswordTextBox")!;
    private TextBox RegisterPhoneNumberTextBoxControl => AuthViewRootControl.FindControl<TextBox>("RegisterPhoneNumberTextBox")!;
    private Button LoginButtonControl => AuthViewRootControl.FindControl<Button>("LoginButton")!;
    private Button RegisterButtonControl => AuthViewRootControl.FindControl<Button>("RegisterButton")!;
    private Button GoToRegisterButtonControl => AuthViewRootControl.FindControl<Button>("GoToRegisterButton")!;
    private Button BackToLoginButtonControl => AuthViewRootControl.FindControl<Button>("BackToLoginButton")!;
    private TextBlock StatusTextBlockControl => AuthViewRootControl.FindControl<TextBlock>("StatusTextBlock")!;
    private TextBlock RegisterStatusTextBlockControl => AuthViewRootControl.FindControl<TextBlock>("RegisterStatusTextBlock")!;

    private void ConfigureAuthViewEvents()
    {
        LoginButtonControl.Click += LoginButton_Click;
        RegisterButtonControl.Click += RegisterButton_Click;
        GoToRegisterButtonControl.Click += GoToRegisterButton_Click;
        BackToLoginButtonControl.Click += BackToLoginButton_Click;
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
        LoginViewControl.IsVisible = false;
        RegisterViewControl.IsVisible = true;
        RegisterStatusTextBlockControl.Text = string.Empty;
    }

    private void ShowLoginView()
    {
        RegisterViewControl.IsVisible = false;
        LoginViewControl.IsVisible = true;
    }

    private void ShowMainView()
    {
        LoginViewControl.IsVisible = false;
        RegisterViewControl.IsVisible = false;
        MainView.IsVisible = true;
        ShowSearchSection();
    }

    private void ShowBrowserLoginView()
    {
        LoginViewControl.IsVisible = false;
        RegisterViewControl.IsVisible = false;
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
        StatusTextBlockControl.Text = message;

        if (RegisterViewControl.IsVisible)
            RegisterStatusTextBlockControl.Text = message;
    }
}

using Yolcu360Otomasyon.Models;

namespace Yolcu360Otomasyon.Services.Auth;

public sealed class AuthWorkflowService
{
    private readonly DatabaseService _databaseService;
    private readonly SmsReceiverService _smsReceiverService;
    private readonly Func<BAService> _getBAService;
    private readonly Action<string> _report;
    private readonly Action _showBrowserLogin;

    public AuthWorkflowService(
        DatabaseService databaseService,
        SmsReceiverService smsReceiverService,
        Func<BAService> getBAService,
        Action<string> report,
        Action showBrowserLogin)
    {
        _databaseService = databaseService;
        _smsReceiverService = smsReceiverService;
        _getBAService = getBAService;
        _report = report;
        _showBrowserLogin = showBrowserLogin;
    }

    public async Task<AuthWorkflowResult> LoginAsync(string email, string password, bool forceBrowserLogin = false)
    {
        _report("Kullanıcı bilgileri kontrol ediliyor...");
        var user = await _databaseService.GetUserByCredentialsAsync(email, password);
        if (user is null)
            return AuthWorkflowResult.Failed("Kullanıcı bulunamadı veya şifre hatalı.");

        var sessionStatePath = AppPaths.BuildSessionStatePath(email);
        if (!forceBrowserLogin && File.Exists(sessionStatePath))
        {
            _report("Kayıtlı oturum bulundu.");
            return new AuthWorkflowResult(
                true,
                BuildActiveUser(user, email, password, sessionStatePath),
                UsedSavedSession: true);
        }

        _showBrowserLogin();
        var baService = await RunPhoneLoginAsync(user.PhoneNumber);
        var code = await WaitForSmsCodeAsync();
        await SubmitSmsCodeAndWaitForLoginAsync(baService, code);
        await SaveLoginSessionAsync(baService, user, email, password, sessionStatePath);

        return new AuthWorkflowResult(
            true,
            BuildActiveUser(user, email, password, sessionStatePath));
    }

    private async Task<BAService> RunPhoneLoginAsync(string phoneNumber)
    {
        _report("Gömülü tarayıcı hazırlanıyor...");

        var baService = _getBAService();
        await baService.ClearBrowserSessionAsync();

        _report("Yolcu360 giriş ekranı dolduruluyor...");
        _smsReceiverService.ClearLatestCode();
        await baService.LoginWithPhoneAsync(phoneNumber);

        return baService;
    }

    private async Task<string> WaitForSmsCodeAsync()
    {
        _report("SMS doğrulama kodu bekleniyor...");

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

    private async Task SubmitSmsCodeAndWaitForLoginAsync(BAService baService, string code)
    {
        _report($"SMS kodu alındı: {code}");
        await baService.FillSmsVerificationCodeAsync(code);
        _report("Girişin tamamlanması bekleniyor...");
        await baService.WaitForLoginCompletedAsync();
    }

    private async Task SaveLoginSessionAsync(
        BAService baService,
        AppUser user,
        string email,
        string password,
        string sessionStatePath)
    {
        _report("Oturum kaydediliyor...");
        await baService.SaveSessionAsync(sessionStatePath);
        await _databaseService.SaveOrUpdateUserAsync(email, password, user.PhoneNumber, sessionStatePath);
    }

    private static AppUser BuildActiveUser(AppUser user, string email, string password, string sessionStatePath)
    {
        return new AppUser
        {
            Id = user.Id,
            Email = email,
            Password = password,
            PhoneNumber = user.PhoneNumber,
            SessionStatePath = sessionStatePath
        };
    }
}

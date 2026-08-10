namespace Yolcu360Otomasyon.Services;

public sealed partial class BAService
{
    public async Task LoginWithPhoneAsync(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            throw new InvalidOperationException("Telefon numarası boş bırakılamaz.");

        await OpenPhoneLoginPageAsync();
        await WaitForPhoneInputAsync();
        var normalizedPhone = NormalizePhoneNumber(phoneNumber);
        await FillPhoneNumberHumanLikeAsync(normalizedPhone);
        await WarmUpRecaptchaScoreAsync(4);
        await ClickContinueButtonHumanLikeAsync();
        await RetryContinueAfterRecaptchaIfNeededAsync();
        await WaitForSmsVerificationScreenAsync();
    }

    public async Task WaitForLoginCompletedAsync(TimeSpan? timeout = null)
    {
        var deadline = DateTimeOffset.UtcNow + (timeout ?? TimeSpan.FromSeconds(30));
        Report("Giriş işleminin tamamlanması bekleniyor...");

        while (DateTimeOffset.UtcNow < deadline)
        {
            var isCompleted = await EvaluateScriptAsync(
                """
                (() => {
                    const url = window.location.href;
                    const text = (document.body.innerText || '').toLocaleLowerCase('tr-TR');
                    return !url.includes('login') || text.includes('hesabım') || text.includes('profil') || text.includes('hoş geldin');
                })();
                """);

            if (IsScriptTrue(isCompleted))
            {
                Report("Giriş başarıyla tamamlandı.");
                return;
            }

            await Task.Delay(500);
        }

        Report("Giriş tamamlanma kontrolü zaman aşımına uğradı, ancak devam ediliyor.");
    }

}

namespace Yolcu360Otomasyon.Services;

public sealed partial class BAService
{
    private async Task OpenPhoneLoginPageAsync()
    {
        Report("Gömülü tarayıcıda Yolcu360 login sayfası açılıyor...");
        await NavigateAsync("https://www.yolcu360.com/login?redirect=%2F");
        await WaitForDocumentReadyAsync();
        await InjectStealthAndHumanMouseScriptAsync();
        await Task.Delay(Random.Shared.Next(1500, 2500));
        await CloseInitialPopupAsync();
    }

    private async Task WaitForPhoneInputAsync()
    {
        Report("Telefon numarası inputu bekleniyor...");
        await WaitForScriptTrueAsync(
            """
            (() => !!document.querySelector('#phn-input') || !!document.querySelector('input[type="tel"]'))();
            """,
            TimeSpan.FromSeconds(20));

        await InjectStealthAndHumanMouseScriptAsync();
    }
}

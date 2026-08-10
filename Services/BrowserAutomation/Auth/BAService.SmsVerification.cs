namespace Yolcu360Otomasyon.Services;

public sealed partial class BAService
{
    public async Task FillSmsVerificationCodeAsync(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new InvalidOperationException("SMS doğrulama kodu boş olamaz.");

        Report($"Gömülü tarayıcıda SMS kodu yazılıyor: {code.Trim()}");
        await Task.Delay(Random.Shared.Next(800, 1400));

        var cleanCode = code.Trim();
        var fillResultJson = await FillSmsCodeInputAsync(cleanCode);
        Report($"SMS kutu dolum sonucu: {fillResultJson}");

        Report("SMS kodu yazıldı, doğrulama butonuna basmadan önce 3.5 saniye bekleniyor...");
        await Task.Delay(Random.Shared.Next(3200, 4200));

        Report("SMS doğrulama butonu tıklanıyor...");
        var clickResult = await ClickSmsVerificationButtonAsync();
        Report(IsScriptTrue(clickResult) ? "SMS doğrulama butonu tıklandı." : "SMS doğrulama butonu bulunamadı, gömülü tarayıcıdan manuel tıklayabilirsiniz.");
    }
}

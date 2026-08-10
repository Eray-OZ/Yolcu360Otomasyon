namespace Yolcu360Otomasyon.Services;

public sealed partial class BAService
{
    public async Task OpenYolcu360HomeAsync()
    {
        Report("Yolcu360 ana sayfası açılıyor...");
        await NavigateAsync(Yolcu360HomeUrl);
        Report("Sayfanın hazır olması bekleniyor...");
        await WaitForDocumentReadyAsync();
        Report("Başlangıç popup'ı bekleniyor...");
        await Task.Delay(InitialPopupDelay);
        var popupClosed = await CloseInitialPopupAsync();
        Report(popupClosed ? "Başlangıç popup'ı kapatıldı." : "Başlangıç popup'ı görünmedi.");
    }
}

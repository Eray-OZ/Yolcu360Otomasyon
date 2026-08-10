namespace Yolcu360Otomasyon.Services;

public sealed partial class BAService
{
    public async Task ClickSearchButtonAsync()
    {
        Report("Araç Ara butonuna tıklanıyor...");
        await EnsureEmbeddedClickHelperAsync();

        await EvaluateScriptAsync(
            """
            (() => {
                if (document.activeElement && typeof document.activeElement.blur === 'function') {
                    document.activeElement.blur();
                }
                const menus = document.querySelectorAll('.dp__menu, .search-autocomplete');
                menus.forEach(m => {
                    if (m.style) m.style.display = 'none';
                });
            })();
            """);

        await Task.Delay(SearchButtonPreparationDelay);

        var clicked = await ClickButtonByTextAsync("ara", "#search");
        if (!clicked)
            throw new InvalidOperationException("Araç Ara butonu tıklanamadı.");

        Report("Araç Ara buton tıklama sonucu: başarılı");
        await Task.Delay(SearchButtonAfterClickDelay);
    }
}

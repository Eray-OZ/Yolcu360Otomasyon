namespace Yolcu360Otomasyon.Services;

public sealed partial class BAService
{
    public async Task FillPickupLocationAsync(string location)
    {
        if (string.IsNullOrWhiteSpace(location))
            throw new InvalidOperationException("Alış yeri boş bırakılamaz.");

        var normalizedLocation = location.Trim();
        var diagnostic = await GetSearchDomDiagnosticAsync();
        Report($"Gömülü DOM: {diagnostic}");

        Report("Alış yeri inputu bekleniyor...");
        await WaitForPickupLocationInputAsync();

        Report($"Alış yeri yazılıyor: {location}");
        await TypePickupLocationAsync(normalizedLocation);

        Report("Alış yeri önerileri bekleniyor...");
        await WaitForLocationSuggestionsAsync(LocationSuggestionSelector, TimeSpan.FromSeconds(12));

        var selectionApplied = await SelectPickupLocationSuggestionWithRetriesAsync(normalizedLocation);
        if (!selectionApplied)
            throw new InvalidOperationException("Alış yeri önerisi seçilemedi.");

        Report("Alış yeri önerisi seçildi.");
    }

    private async Task WaitForPickupLocationInputAsync()
    {
        var pickupLocationInputSelectorJson = ToJson(PickupLocationInputSelector);
        await WaitForScriptTrueAsync(
            $$"""
            (() => !!document.querySelector({{pickupLocationInputSelectorJson}}))();
            """,
            TimeSpan.FromSeconds(20));
    }

    private async Task TypePickupLocationAsync(string location)
    {
        var locationJson = ToJson(location);
        var pickupLocationInputSelectorJson = ToJson(PickupLocationInputSelector);

        await EvaluateScriptAsync(
            $$"""
            (() => {
                const input = document.querySelector({{pickupLocationInputSelectorJson}});
                const text = {{locationJson}};
                input.focus();
                input.value = '';
                input.dispatchEvent(new InputEvent('input', { bubbles: true, inputType: 'deleteContentBackward', data: null }));

                for (const char of text) {
                    input.dispatchEvent(new KeyboardEvent('keydown', { bubbles: true, key: char }));
                    input.value += char;
                    input.dispatchEvent(new InputEvent('input', { bubbles: true, inputType: 'insertText', data: char }));
                    input.dispatchEvent(new KeyboardEvent('keyup', { bubbles: true, key: char }));
                }

                input.dispatchEvent(new Event('change', { bubbles: true }));
                return true;
            })();
            """);
    }

    private async Task<bool> SelectPickupLocationSuggestionWithRetriesAsync(string location)
    {
        var selectionApplied = false;
        for (var attempt = 1; attempt <= 3 && !selectionApplied; attempt++)
        {
            Report($"Alış yeri önerisi seçiliyor. Deneme: {attempt}");
            var selected = await SelectBestPickupLocationSuggestionAsync(location);

            Report($"Alış yeri seçim sonucu: {selected}");
            await Task.Delay(LocationSelectionApplyDelay);
            selectionApplied = await IsPickupLocationSelectionAppliedAsync();
        }

        return selectionApplied;
    }
}

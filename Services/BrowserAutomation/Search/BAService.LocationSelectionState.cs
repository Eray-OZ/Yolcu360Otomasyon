namespace Yolcu360Otomasyon.Services;

public sealed partial class BAService
{
    private async Task<bool> IsPickupLocationSelectionAppliedAsync()
    {
        var pickupLocationInputSelectorJson = ToJson(PickupLocationInputSelector);
        var locationSuggestionSelectorJson = ToJson(LocationSuggestionSelector);
        var result = await EvaluateScriptAsync(
            $$"""
            (() => {
                const input = document.querySelector({{pickupLocationInputSelectorJson}});
                const visibleSuggestions = Array.from(document.querySelectorAll({{locationSuggestionSelectorJson}}))
                    .filter(item => {
                        const rect = item.getBoundingClientRect();
                        const style = getComputedStyle(item);
                        return rect.width > 0 &&
                            rect.height > 0 &&
                            style.display !== 'none' &&
                            style.visibility !== 'hidden';
                    });
                return !!input && input.value.trim().length > 0 && visibleSuggestions.length === 0;
            })();
            """);

        return IsScriptTrue(result);
    }
}

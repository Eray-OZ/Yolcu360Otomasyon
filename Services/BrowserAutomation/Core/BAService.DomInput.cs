namespace Yolcu360Otomasyon.Services;

public sealed partial class BAService
{
    private async Task<bool> SetInputValueAsync(string selector, string value, bool blurAfterChange = true)
    {
        var selectorJson = ToJson(selector);
        var valueJson = ToJson(value);
        var blurAfterChangeJson = ToJson(blurAfterChange);

        return await EvaluateBooleanScriptAsync(
            $$"""
            (() => {
                const input = document.querySelector({{selectorJson}});
                if (!input) return false;
                input.focus();

                const proto = input instanceof HTMLInputElement ? Object.getPrototypeOf(input) : null;
                const desc = proto ? Object.getOwnPropertyDescriptor(proto, 'value') : null;
                if (desc && desc.set) {
                    desc.set.call(input, {{valueJson}});
                } else {
                    input.value = {{valueJson}};
                }

                input.dispatchEvent(new Event('input', { bubbles: true }));
                input.dispatchEvent(new Event('change', { bubbles: true }));
                if ({{blurAfterChangeJson}}) {
                    input.dispatchEvent(new Event('blur', { bubbles: true }));
                }
                return true;
            })();
            """);
    }
}

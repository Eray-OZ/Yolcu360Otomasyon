namespace Yolcu360Otomasyon.Services;

public sealed partial class BAService
{
    private async Task<bool> ClickCalendarNavAsync(bool forward)
    {
        var forwardJson = ToJson(forward);
        var result = await EvaluateScriptAsync(
            $$"""
            (() => {
                const forward = {{forwardJson}};
                const next = document.querySelector("[data-dp-element='action-next'], .dp__next_btn, button[aria-label*='Next']");
                const prev = document.querySelector("[data-dp-element='action-prev'], .dp__prev_btn, button[aria-label*='Prev']");
                const navBtns = Array.from(document.querySelectorAll('.dp__nav_btn'));

                const btn = forward
                    ? (next || (navBtns.length > 1 ? navBtns[navBtns.length - 1] : navBtns[0]))
                    : (prev || navBtns[0]);

                if (!btn) return false;

                btn.click();
                return true;
            })();
            """);

        return IsScriptTrue(result);
    }

    private static bool IsTargetMonthVisible(string headerText, DateTime target)
    {
        if (string.IsNullOrWhiteSpace(headerText))
            return false;

        var monthName = TurkishMonthNames[target.Month - 1];
        var yearStr = target.Year.ToString();

        return headerText.Contains(monthName, StringComparison.OrdinalIgnoreCase)
            && headerText.Contains(yearStr);
    }

    private static bool ShouldGoBack(string headerText, DateTime target)
    {
        if (string.IsNullOrWhiteSpace(headerText))
            return false;

        foreach (var part in headerText.Split(' '))
        {
            if (int.TryParse(part, out var year))
            {
                if (year > target.Year) return true;
                if (year < target.Year) return false;
                break;
            }
        }

        for (var i = 0; i < TurkishMonthNames.Length; i++)
        {
            if (headerText.Contains(TurkishMonthNames[i], StringComparison.OrdinalIgnoreCase))
                return (i + 1) > target.Month;
        }

        return false;
    }
}

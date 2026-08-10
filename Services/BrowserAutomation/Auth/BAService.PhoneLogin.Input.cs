namespace Yolcu360Otomasyon.Services;

public sealed partial class BAService
{
    private async Task FillPhoneNumberHumanLikeAsync(string normalizedPhone)
    {
        Report($"Telefon numarası insansı davranışla yazılıyor: {normalizedPhone}");

        await Task.Delay(350);
        await ClearPhoneInputAsync();
        await Task.Delay(220);

        var phoneChunks = SplitPhoneNumber(normalizedPhone);
        foreach (var chunk in phoneChunks)
        {
            foreach (var ch in chunk)
            {
                await TypePhoneCharacterAsync(ch);
                await Task.Delay(Random.Shared.Next(110, 170));
            }
            await Task.Delay(Random.Shared.Next(180, 320));
        }

        await FinalizePhoneInputAsync();
    }

    private Task ClearPhoneInputAsync()
    {
        return EvaluateScriptAsync(
            """
            (() => {
                const input = document.querySelector('#phn-input') || document.querySelector('input[type="tel"]');
                if (!input) return false;
                input.scrollIntoView({ block: 'center', inline: 'nearest' });
                input.focus();
                input.click();
                input.value = '';
                input.dispatchEvent(new Event('input', { bubbles: true }));
                return true;
            })();
            """);
    }

    private Task TypePhoneCharacterAsync(char ch)
    {
        var charJson = ToJson(ch.ToString());
        return EvaluateScriptAsync(
            $$"""
            (() => {
                const input = document.querySelector('#phn-input') || document.querySelector('input[type="tel"]');
                if (!input) return false;
                const char = {{charJson}};
                input.dispatchEvent(new KeyboardEvent('keydown', { bubbles: true, key: char }));
                input.value = (input.value || '') + char;
                input.dispatchEvent(new InputEvent('input', { bubbles: true, inputType: 'insertText', data: char }));
                input.dispatchEvent(new Event('input', { bubbles: true }));
                input.dispatchEvent(new KeyboardEvent('keyup', { bubbles: true, key: char }));
                return true;
            })();
            """);
    }

    private Task FinalizePhoneInputAsync()
    {
        return EvaluateScriptAsync(
            """
            (() => {
                const input = document.querySelector('#phn-input') || document.querySelector('input[type="tel"]');
                if (!input) return;
                input.dispatchEvent(new Event('change', { bubbles: true }));
                input.dispatchEvent(new Event('blur', { bubbles: true }));

                const btn = Array.from(document.querySelectorAll('button, input[type="submit"]'))
                    .find(b => (b.textContent || b.value || '').trim().toLowerCase().includes('devam'));
                if (btn) {
                    btn.disabled = false;
                    btn.removeAttribute('disabled');
                    btn.classList.remove('disabled');
                }
            })();
            """);
    }
}

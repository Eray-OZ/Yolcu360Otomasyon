namespace Yolcu360Otomasyon.Services;

public sealed partial class BAService
{
    public async Task SelectTimeAsync(int timePickerIndex, string time)
    {
        if (string.IsNullOrWhiteSpace(time)) return;

        Report($"Saat seçimi yapılıyor (index {timePickerIndex}): {time}");
        var opened = await OpenTimePickerAsync(timePickerIndex);

        if (!IsScriptTrue(opened))
        {
            Report($"Saat kutusu [{timePickerIndex}] tetiklenemedi veya açılamadı.");
            return;
        }

        await Task.Delay(TimePickerOpenDelay);

        var selected = await SelectTimeOptionAsync(time.Trim());

        if (IsScriptTrue(selected))
        {
            Report($"Saat seçildi: {time}");
        }
        else
        {
            Report($"Saat '{time}' seçeneklerde bulunamadı.");
        }

        await Task.Delay(TimePickerSelectionDelay);
    }
}

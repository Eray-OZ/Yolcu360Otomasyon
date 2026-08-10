namespace Yolcu360Otomasyon.Services;

public sealed partial class BAService
{
    public async Task SelectDateRangeAsync(DateTime pickupDate, DateTime returnDate)
    {
        Report($"Alış ve Bırakış tarihleri seçiliyor: {pickupDate:dd.MM.yyyy} – {returnDate:dd.MM.yyyy}");

        Report("Tarih seçici açılıyor...");
        var opened = await OpenDatePickerAsync();
        if (!opened)
            throw new InvalidOperationException("Tarih seçici (datepicker) açılamadı.");

        Report("Tarih takvimi bekleniyor...");
        await WaitForDatePickerMenuAsync(TimeSpan.FromSeconds(10));

        Report($"Alış tarihi için ay kontrol ediliyor: {pickupDate:MMMM yyyy}");
        await NavigateToMonthAsync(pickupDate);
        await Task.Delay(DatePickerActionDelay);

        Report($"Alış tarihi seçiliyor: {pickupDate:dd.MM.yyyy}");
        var pickupSelected = await ClickCalendarDayAsync(pickupDate);
        if (!pickupSelected)
            throw new InvalidOperationException($"Alış tarihi ({pickupDate:dd.MM.yyyy}) takvimde seçilemedi.");

        Report($"Alış tarihi seçildi: {pickupDate:dd.MM.yyyy}");
        await Task.Delay(DatePickerSelectionDelay);

        if (returnDate.Year != pickupDate.Year || returnDate.Month != pickupDate.Month)
        {
            Report($"Bırakış tarihi için ay geziliyor: {returnDate:MMMM yyyy}");
            await NavigateToMonthAsync(returnDate);
            await Task.Delay(DatePickerActionDelay);
        }

        Report($"Bırakış tarihi seçiliyor: {returnDate:dd.MM.yyyy}");
        var returnSelected = await ClickCalendarDayAsync(returnDate);
        if (!returnSelected)
            throw new InvalidOperationException($"Bırakış tarihi ({returnDate:dd.MM.yyyy}) takvimde seçilemedi.");

        Report($"Bırakış tarihi seçildi: {returnDate:dd.MM.yyyy}");
        await Task.Delay(DatePickerSelectionDelay);

        await ConfirmDatePickerAsync();
        await Task.Delay(DatePickerActionDelay);
    }
}

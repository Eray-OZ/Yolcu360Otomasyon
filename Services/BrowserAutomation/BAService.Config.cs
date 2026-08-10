namespace Yolcu360Otomasyon.Services;

public sealed partial class BAService
{
    private const string Yolcu360HomeUrl = "https://www.yolcu360.com/";
    private const string PickupLocationInputSelector = "#inputPickUpLocation";
    private const string LocationSuggestionSelector = ".search-autocomplete__item, .search-autocomplete-mobile__item, .search-autocomplete .location-item, .location-item";
    private const string DateTimeGroupSelector = "[modaltitle='Alış ve Bırakış Tarihi']";
    private const string DatePickerSelector = ".dp__main.dp__theme_light";

    private static readonly TimeSpan InitialPopupDelay = TimeSpan.FromMilliseconds(2500);
    private static readonly TimeSpan ScriptPollingDelay = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan FilterPanelReadyDelay = TimeSpan.FromMilliseconds(1200);
    private static readonly TimeSpan FilterRefreshDelay = TimeSpan.FromMilliseconds(1000);
    private static readonly TimeSpan ResultsRefreshDelay = TimeSpan.FromMilliseconds(1500);
    private static readonly TimeSpan ResultsPollingDelay = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan LocationSelectionApplyDelay = TimeSpan.FromMilliseconds(700);
    private static readonly TimeSpan DatePickerActionDelay = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan DatePickerSelectionDelay = TimeSpan.FromMilliseconds(400);
    private static readonly TimeSpan DatePickerMenuPollingDelay = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan CalendarNavigationDelay = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan TimePickerOpenDelay = TimeSpan.FromMilliseconds(400);
    private static readonly TimeSpan TimePickerSelectionDelay = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan SearchButtonPreparationDelay = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan SearchButtonAfterClickDelay = TimeSpan.FromMilliseconds(1000);
    private static readonly TimeSpan LogoutNavigationDelay = TimeSpan.FromMilliseconds(1200);
    private static readonly TimeSpan PaymentPageHydrationDelay = TimeSpan.FromMilliseconds(2000);
    private static readonly TimeSpan PaymentTabSelectionDelay = TimeSpan.FromMilliseconds(600);
    private static readonly TimeSpan PaymentFormSubmitPreparationDelay = TimeSpan.FromMilliseconds(1250);
}

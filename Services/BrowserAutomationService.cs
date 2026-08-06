using System.Text.Json.Serialization;
using PuppeteerSharp;

namespace Yolcu360Otomasyon.Services;

public sealed partial class BrowserAutomationService : IAsyncDisposable
{
    private const string Yolcu360HomeUrl = "https://www.yolcu360.com/";
    private const string LoginRecaptchaEndpoint = "/api/v1/accounts-api/auth/login/phone/code/recaptcha/";
    private const string DefaultSessionStateFilePath = "/Users/erayoz/Codes/Staj/Yolcu360Otomasyon/session_state.json";
    private const string ChromeExecutablePath = "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome";
    private const string ChromeSourceUserDataDir = "/Users/erayoz/Library/Application Support/Google/Chrome";
    private const string ChromeUserDataDir = "/Users/erayoz/Codes/Staj/Yolcu360Otomasyon/chrome-user-profile";
    private const string ChromeProfileDirectory = "Default";

    private IBrowser? _browser;
    private IPage? _page;
    private readonly string _sessionStateFilePath;

    public event Action<string>? ProgressChanged;

    public BrowserAutomationService(string? sessionStateFilePath = null)
    {
        _sessionStateFilePath = string.IsNullOrWhiteSpace(sessionStateFilePath)
            ? DefaultSessionStateFilePath
            : sessionStateFilePath;
    }

    private sealed class AppliedFilterResult
    {
        [JsonPropertyName("applied")]
        public bool Applied { get; set; }

        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;
    }

    private static class Selectors
    {
        public const string LoginPagePhoneInput = "#phn-input";
        public const string LoginPageContinueButton = "button";
        public const string PickupLocationInput = "#inputPickUpLocation";
        public const string AllDatePickers = ".dp__main.dp__theme_light";
        public const string PickupDateContainer = "[modaltitlecmskey='pickup_and_dropoff_date'] .dp__main.dp__theme_light";
        public const string DatePickerMenu = ".dp__menu";
        public const string DatePickerNextMonth = ".dp__nav_btn[data-dp-element='action-next'], .dp__next_btn, button[aria-label*='Next']";
        public const string DatePickerPrevMonth = ".dp__nav_btn[data-dp-element='action-prev'], .dp__prev_btn, button[aria-label*='Prev']";
        public const string DatePickerMonthYear = ".dp__month_year_select, .dp__calendar_header_item--current, .dp__action_select";
        public const string SearchButton = "#search";
    }

    private sealed class ClickPoint
    {
        [JsonPropertyName("found")]
        public bool Found { get; init; }

        [JsonPropertyName("enabled")]
        public bool Enabled { get; init; }

        [JsonPropertyName("x")]
        public decimal X { get; init; }

        [JsonPropertyName("y")]
        public decimal Y { get; init; }

        [JsonPropertyName("text")]
        public string Text { get; init; } = "";

        [JsonPropertyName("index")]
        public int Index { get; init; }
    }

    private sealed class SessionState
    {
        public DateTimeOffset SavedAt { get; init; }
        public string CurrentUrl { get; init; } = "";
        public CookieParam[] Cookies { get; init; } = [];
        public Dictionary<string, string?> LocalStorage { get; init; } = [];
        public Dictionary<string, string?> SessionStorage { get; init; } = [];
    }
}

using Avalonia.Controls;

namespace Yolcu360Otomasyon;

public partial class MainWindow : Window
{
    private void ShowSearchSection()
    {
        ShowContentSection(
            visiblePanel: SearchPanelControl,
            activeButton: SearchTabButton,
            showSearchResults: _latestResults is not null && _latestResults.Count > 0);
    }

    private void ShowHistorySection()
    {
        ShowContentSection(HistoryPanelControl, HistoryTabButton);
    }

    private void ShowPaymentsSection()
    {
        ShowContentSection(PaymentsPanelControl, PaymentsTabButton);
    }

    private void ShowPaymentCheckoutSection()
    {
        ShowContentSection(PaymentCheckoutPanelControl, PaymentsTabButton);
    }

    private void ShowBrowserSection()
    {
        ShowContentSection(BrowserSectionPanelControl, NativeWebViewTestButton, showNativeWebViewTest: true);
    }

    private void ShowContentSection(
        Control visiblePanel,
        Button activeButton,
        bool showSearchResults = false,
        bool showNativeWebViewTest = false)
    {
        SearchViewRootControl.IsVisible = ReferenceEquals(visiblePanel, SearchPanelControl);
        SearchPanelControl.IsVisible = ReferenceEquals(visiblePanel, SearchPanelControl);
        SearchResultsPanelControl.IsVisible = showSearchResults;
        HistoryViewRootControl.IsVisible = ReferenceEquals(visiblePanel, HistoryPanelControl);
        HistoryPanelControl.IsVisible = ReferenceEquals(visiblePanel, HistoryPanelControl);
        PaymentsViewRootControl.IsVisible =
            ReferenceEquals(visiblePanel, PaymentsPanelControl) ||
            ReferenceEquals(visiblePanel, PaymentCheckoutPanelControl);
        PaymentsPanelControl.IsVisible = ReferenceEquals(visiblePanel, PaymentsPanelControl);
        PaymentCheckoutPanelControl.IsVisible = ReferenceEquals(visiblePanel, PaymentCheckoutPanelControl);
        BrowserViewRootControl.IsVisible = ReferenceEquals(visiblePanel, BrowserSectionPanelControl);
        BrowserSectionPanelControl.IsVisible = ReferenceEquals(visiblePanel, BrowserSectionPanelControl);

        SearchTabButton.Classes.Set("primary", ReferenceEquals(activeButton, SearchTabButton));
        HistoryTabButton.Classes.Set("primary", ReferenceEquals(activeButton, HistoryTabButton));
        PaymentsTabButton.Classes.Set("primary", ReferenceEquals(activeButton, PaymentsTabButton));
        NativeWebViewTestButton.Classes.Set("primary", ReferenceEquals(activeButton, NativeWebViewTestButton));
        NativeWebViewTestButton.IsVisible = showNativeWebViewTest;
    }
}

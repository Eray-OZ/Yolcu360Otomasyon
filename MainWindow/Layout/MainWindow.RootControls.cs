using Avalonia.Controls;
using Yolcu360Otomasyon.Views;

namespace Yolcu360Otomasyon;

public partial class MainWindow
{
    private AuthView AuthViewRootControl => this.FindControl<AuthView>("AuthViewControl")!;
    private SearchView SearchViewRootControl => this.FindControl<SearchView>("SearchViewControl")!;
    private BrowserView BrowserViewRootControl => this.FindControl<BrowserView>("BrowserViewControl")!;
    private HistoryView HistoryViewRootControl => this.FindControl<HistoryView>("HistoryViewControl")!;
    private PaymentsView PaymentsViewRootControl => this.FindControl<PaymentsView>("PaymentsViewControl")!;
}

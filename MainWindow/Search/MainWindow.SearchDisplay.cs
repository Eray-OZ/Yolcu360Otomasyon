using Avalonia.Threading;
using Yolcu360Otomasyon.Models;

namespace Yolcu360Otomasyon;

public partial class MainWindow
{
    private async Task DisplaySearchResultsAsync(List<SearchResultItem> results)
    {
        _latestResults = results;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            ResultsDataGridControl.ItemsSource = null;
            ResultsDataGridControl.ItemsSource = _latestResults;
            SearchResultsPanelControl.IsVisible = _latestResults.Count > 0;
        });

        SetSearchStatus(_latestResults.Count == 0
            ? "Arama tamamlandı, sonuç bulunamadı."
            : $"{_latestResults.Count} sonuç listelendi. İlk sonuç: {_latestResults[0].Title} | {_latestResults[0].Price}");
    }
}

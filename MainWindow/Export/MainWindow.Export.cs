using Yolcu360Otomasyon.Models;

namespace Yolcu360Otomasyon;

public partial class MainWindow
{
    private Task<string> ExportHistorySelectionAsPngAsync(IReadOnlyList<KoleksiyonListItem> collections)
    {
        return _collectionPngExportService.ExportHistorySelectionAsPngAsync(collections, SetHistoryStatus);
    }
}

using Avalonia.Threading;
using Yolcu360Otomasyon.Models;

namespace Yolcu360Otomasyon.Services;

public sealed partial class CollectionPngExportService
{
    private const double CollectionReportWidth = 1440;

    private readonly DatabaseService _databaseService;

    public CollectionPngExportService(DatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public async Task<string> ExportHistorySelectionAsPngAsync(
        IReadOnlyList<KoleksiyonListItem> collections,
        Action<string> updateStatus)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            updateStatus(collections.Count == 1
                ? $"{collections[0].OzelAd} PNG olarak hazırlanıyor..."
                : $"{collections.Count} kayıt için PNG hazırlanıyor...");
        });

        var collectionsWithVehicles = await LoadCollectionsWithVehiclesAsync(collections);
        var filePath = BuildHistoryExportPath(collections);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var report = BuildCollectionReportVisual(collectionsWithVehicles);
            RenderControlToPng(report, filePath, CollectionReportWidth);
        });

        return filePath;
    }

    private async Task<List<(KoleksiyonListItem Collection, List<SearchResultItem> Vehicles)>> LoadCollectionsWithVehiclesAsync(
        IReadOnlyList<KoleksiyonListItem> collections)
    {
        var tasks = collections.Select(async collection =>
        {
            var vehicles = await _databaseService.GetCollectionVehiclesAsync(collection.Id);
            return (Collection: collection, Vehicles: vehicles);
        });

        return (await Task.WhenAll(tasks)).ToList();
    }

    private static string BuildHistoryExportPath(IReadOnlyList<KoleksiyonListItem> collections)
    {
        var downloadsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads");

        Directory.CreateDirectory(downloadsDirectory);

        var baseName = collections.Count == 1 ? collections[0].OzelAd : $"{collections.Count}_kayit";
        var safeName = string.Concat(baseName.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_'));
        return Path.Combine(downloadsDirectory, $"{safeName}_{DateTime.Now:yyyyMMdd_HHmmss}.png");
    }
}

using Avalonia.Interactivity;

namespace Yolcu360Otomasyon;

public partial class MainWindow
{
    private async void DeleteCollectionButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_activeUser is null || _selectedCollections.Count == 0)
        {
            SetHistoryStatus("Silmek için bir kayıt seçin.");
            return;
        }

        DeleteCollectionButtonControl.IsEnabled = false;
        try
        {
            foreach (var collection in _selectedCollections)
                await _databaseService.DeleteCollectionAsync(collection.Id, _activeUser.Id);

            ClearSelectedCollectionState();
            await LoadHistoryAsync();
            SetHistoryStatus("Seçili kayıtlar silindi.");
        }
        catch (Exception ex)
        {
            SetHistoryStatus($"Silme hatası: {ex.Message}");
        }
        finally
        {
            DeleteCollectionButtonControl.IsEnabled = true;
        }
    }

    private async void ExportPngButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedCollections.Count == 0)
        {
            SetHistoryStatus("PNG indirmek için bir kayıt seçin.");
            return;
        }

        ExportPngButtonControl.IsEnabled = false;
        try
        {
            var filePath = await ExportHistorySelectionAsPngAsync(_selectedCollections);
            SetHistoryStatus($"PNG kaydedildi: {filePath}");
        }
        catch (Exception ex)
        {
            SetHistoryStatus($"PNG oluşturma hatası: {ex.Message}");
        }
        finally
        {
            ExportPngButtonControl.IsEnabled = true;
        }
    }
}

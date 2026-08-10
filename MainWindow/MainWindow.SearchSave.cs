using Avalonia.Interactivity;
using Yolcu360Otomasyon.Models;

namespace Yolcu360Otomasyon;

public partial class MainWindow
{
    private async void SaveResultsButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_activeUser is null)
        {
            SetSearchStatus("Önce giriş yapılmalı.");
            return;
        }

        if (_activeUser.Id <= 0)
        {
            var latestUser = await _databaseService.GetUserByEmailAsync(_activeUser.Email);
            if (latestUser is null)
            {
                SetSearchStatus("Aktif kullanıcı veritabanında bulunamadı.");
                return;
            }

            _activeUser = latestUser;
        }

        if (_latestResults.Count == 0)
        {
            SetSearchStatus("Kaydedilecek sonuç yok.");
            return;
        }

        if (_latestSearchFilter is null)
        {
            SetSearchStatus("Önce geçerli bir arama yapılmalı.");
            return;
        }

        var ozelAd = CollectionNameTextBoxControl.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(ozelAd))
        {
            SetSearchStatus("Özel kayıt adı girin.");
            return;
        }

        SaveResultsButtonControl.IsEnabled = false;
        try
        {
            var collectionId = await _databaseService.SaveCollectionAsync(_activeUser.Id, ozelAd, _latestSearchFilter, _latestResults);
            CollectionNameTextBoxControl.Text = string.Empty;
            SetSearchStatus($"{_latestResults.Count} sonuç \"{ozelAd}\" adıyla kaydedildi.");
            await LoadHistoryAsync();
            ShowHistorySection();

            var collections = (CollectionsDataGridControl.ItemsSource as IEnumerable<KoleksiyonListItem>)?.ToList() ?? new List<KoleksiyonListItem>();
            var savedCollection = collections.FirstOrDefault(item => item.Id == collectionId);
            if (savedCollection is not null)
                CollectionsDataGridControl.SelectedItem = savedCollection;
        }
        catch (Exception ex)
        {
            SetSearchStatus($"Kaydetme hatası: {ex.Message}");
        }
        finally
        {
            SaveResultsButtonControl.IsEnabled = true;
        }
    }
}

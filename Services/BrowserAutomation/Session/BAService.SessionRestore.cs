using System.Text.Json;
using Yolcu360Otomasyon.Models;

namespace Yolcu360Otomasyon.Services;

public sealed partial class BAService
{
    public async Task<bool> RestoreSessionAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return false;

        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            var state = JsonSerializer.Deserialize<EmbeddedSessionState>(json);
            if (state is null) return false;

            Report($"Kaydedilmiş oturum gömülü tarayıcıya yükleniyor ({filePath})...");

            if (!string.IsNullOrWhiteSpace(state.Cookies))
                await RestoreCookiesAsync(state.Cookies);

            if (state.LocalStorage.Count > 0)
                await RestoreStorageAsync("localStorage", state.LocalStorage);

            if (state.SessionStorage.Count > 0)
                await RestoreStorageAsync("sessionStorage", state.SessionStorage);

            Report("Kaydedilmiş oturum gömülü tarayıcıya başarıyla restore edildi.");
            return true;
        }
        catch (Exception ex)
        {
            Report($"Oturum yükleme hatası: {ex.Message}");
            return false;
        }
    }

    private async Task RestoreCookiesAsync(string cookies)
    {
        var cookieParts = cookies.Split(';', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in cookieParts)
        {
            var partJson = ToJson(part.Trim() + "; path=/; domain=.yolcu360.com");
            await EvaluateScriptAsync($"document.cookie = {partJson};");
        }
    }

    private Task RestoreStorageAsync(string storageName, Dictionary<string, string?> items)
    {
        var storageNameJson = ToJson(storageName);
        var itemsJson = ToJson(items);
        return EvaluateScriptAsync(
            $$"""
            (() => {
                const storage = window[{{storageNameJson}}];
                const items = {{itemsJson}};
                for (const key in items) {
                    if (Object.prototype.hasOwnProperty.call(items, key)) {
                        storage.setItem(key, items[key]);
                    }
                }
            })();
            """);
    }
}

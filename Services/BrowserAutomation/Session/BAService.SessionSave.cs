using System.Text.Json;
using Yolcu360Otomasyon.Models;

namespace Yolcu360Otomasyon.Services;

public sealed partial class BAService
{
    public async Task SaveSessionAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return;

        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var cookiesRaw = await EvaluateScriptAsync("document.cookie");
            var cookies = (cookiesRaw ?? string.Empty).Trim().Trim('"');
            var localStorage = DeserializeStorage(await ReadStorageJsonAsync("localStorage"));
            var sessionStorage = DeserializeStorage(await ReadStorageJsonAsync("sessionStorage"));

            var currentUrlRaw = await EvaluateScriptAsync("window.location.href");
            var currentUrl = (currentUrlRaw ?? string.Empty).Trim().Trim('"');

            var state = new EmbeddedSessionState
            {
                SavedAt = DateTimeOffset.UtcNow,
                CurrentUrl = currentUrl,
                Cookies = cookies,
                LocalStorage = localStorage,
                SessionStorage = sessionStorage
            };

            var json = ToJson(state, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, json);
            Report($"Oturum gömülü tarayıcıdan dosyaya kaydedildi: {filePath}");
        }
        catch (Exception ex)
        {
            Report($"Oturum kaydetme hatası: {ex.Message}");
        }
    }

    private Task<string?> ReadStorageJsonAsync(string storageName)
    {
        var storageNameJson = ToJson(storageName);
        return EvaluateScriptAsync(
            $$"""
            (() => {
                const result = {};
                const storage = window[{{storageNameJson}}];
                try {
                    for (let i = 0; i < storage.length; i++) {
                        const key = storage.key(i);
                        if (key) result[key] = storage.getItem(key);
                    }
                } catch {}
                return JSON.stringify(result);
            })();
            """);
    }

    private static Dictionary<string, string?> DeserializeStorage(string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
            return new Dictionary<string, string?>();

        try
        {
            var unescaped = rawJson.Trim().Trim('"').Replace("\\\"", "\"");
            return JsonSerializer.Deserialize<Dictionary<string, string?>>(unescaped) ?? new Dictionary<string, string?>();
        }
        catch
        {
            return new Dictionary<string, string?>();
        }
    }
}

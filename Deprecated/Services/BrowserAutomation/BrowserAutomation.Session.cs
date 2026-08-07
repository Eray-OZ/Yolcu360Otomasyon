using System.Text.Json;
using PuppeteerSharp;

namespace Yolcu360Otomasyon.Services;

public sealed partial class BrowserAutomationService
{
    public async Task SaveCurrentSessionAsync()
    {
        var page = GetPage();
        var directory = Path.GetDirectoryName(_sessionStateFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var state = new SessionState
        {
            SavedAt = DateTimeOffset.Now,
            CurrentUrl = page.Url,
            Cookies = await page.GetCookiesAsync(),
            LocalStorage = await ReadStorageAsync("localStorage"),
            SessionStorage = await ReadStorageAsync("sessionStorage")
        };

        var json = JsonSerializer.Serialize(state, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllTextAsync(_sessionStateFilePath, json);
        Report("Oturum kaydedildi.");
    }

    private async Task TryRestoreSessionAsync()
    {
        if (!File.Exists(_sessionStateFilePath))
            return;

        SessionState? state;

        try
        {
            var json = await File.ReadAllTextAsync(_sessionStateFilePath);
            state = JsonSerializer.Deserialize<SessionState>(json);
        }
        catch
        {
            return;
        }

        if (state is null)
            return;

        var page = GetPage();
        await page.GoToAsync(Yolcu360HomeUrl, WaitUntilNavigation.Networkidle2);

        if (state.Cookies.Length > 0)
            await page.SetCookieAsync(state.Cookies);

        await WriteStorageAsync("localStorage", state.LocalStorage);
        await WriteStorageAsync("sessionStorage", state.SessionStorage);
        await page.ReloadAsync(new NavigationOptions
        {
            WaitUntil = [WaitUntilNavigation.Networkidle2]
        });

        Report("Kaydedilmiş oturum yüklendi.");
    }

    public bool HasSavedSession() => File.Exists(_sessionStateFilePath);

    private async Task<Dictionary<string, string?>> ReadStorageAsync(string storageName)
    {
        var page = GetPage();
        return await page.EvaluateFunctionAsync<Dictionary<string, string?>>(
            """
            (storageName) => {
                const storage = window[storageName];
                const result = {};
                if (!storage) return result;

                for (let index = 0; index < storage.length; index++) {
                    const key = storage.key(index);
                    result[key] = storage.getItem(key);
                }

                return result;
            }
            """,
            storageName);
    }

    private async Task WriteStorageAsync(string storageName, Dictionary<string, string?> values)
    {
        var page = GetPage();
        await page.EvaluateFunctionAsync(
            """
            (storageName, values) => {
                const storage = window[storageName];
                if (!storage) return;

                storage.clear();

                for (const [key, value] of Object.entries(values || {})) {
                    if (value === null || value === undefined) continue;
                    storage.setItem(key, value);
                }
            }
            """,
            storageName,
            values);
    }
}

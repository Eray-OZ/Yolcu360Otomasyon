using System.Text.Json;
using Yolcu360Otomasyon.Models;

namespace Yolcu360Otomasyon.Services;

public sealed partial class BAService
{
    public async Task ClearBrowserSessionAsync()
    {
        Report("Gömülü tarayıcı oturumu ve çerezleri temizleniyor...");
        try
        {
            await NavigateAsync("https://www.yolcu360.com/logout");
            await WaitForDocumentReadyAsync(TimeSpan.FromSeconds(10));

            await EvaluateScriptAsync(
                """
                (() => {
                    try {
                        const domains = ['', '.yolcu360.com', 'www.yolcu360.com', 'yolcu360.com', '.google.com', '.recaptcha.net'];
                        const paths = ['/', '/login', '/api'];
                        const cookies = document.cookie.split(";");
                        for (let i = 0; i < cookies.length; i++) {
                            const cookie = cookies[i];
                            const eqPos = cookie.indexOf("=");
                            const name = eqPos > -1 ? cookie.substr(0, eqPos).trim() : cookie.trim();
                            if (!name) continue;

                            for (const d of domains) {
                                for (const p of paths) {
                                    document.cookie = `${name}=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=${p}${d ? `; domain=${d}` : ''}`;
                                }
                            }
                        }
                    } catch {}
                    try { localStorage.clear(); } catch {}
                    try { sessionStorage.clear(); } catch {}
                    try {
                        if (window.indexedDB && window.indexedDB.databases) {
                            window.indexedDB.databases().then(dbs => {
                                for (const db of dbs) {
                                    if (db.name) window.indexedDB.deleteDatabase(db.name);
                                }
                            });
                        }
                    } catch {}
                    return true;
                })();
                """);
            await WaitForScriptTrueOrTimeoutAsync(
                """
                (() => {
                    try {
                        return localStorage.length === 0 && sessionStorage.length === 0;
                    } catch {
                        return true;
                    }
                })();
                """,
                TimeSpan.FromSeconds(3));
            Report("Gömülü tarayıcı çerezleri ve yerel depolama başarıyla temizlendi.");
        }
        catch (Exception ex)
        {
            Report($"Oturum temizleme hatası: {ex.Message}");
        }
    }

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

            var localStorageJson = await EvaluateScriptAsync(
                """
                (() => {
                    const result = {};
                    try {
                        for (let i = 0; i < localStorage.length; i++) {
                            const key = localStorage.key(i);
                            if (key) result[key] = localStorage.getItem(key);
                        }
                    } catch {}
                    return JSON.stringify(result);
                })();
                """);

            var sessionStorageJson = await EvaluateScriptAsync(
                """
                (() => {
                    const result = {};
                    try {
                        for (let i = 0; i < sessionStorage.length; i++) {
                            const key = sessionStorage.key(i);
                            if (key) result[key] = sessionStorage.getItem(key);
                        }
                    } catch {}
                    return JSON.stringify(result);
                })();
                """);

            var localStorage = DeserializeStorage(localStorageJson);
            var sessionStorage = DeserializeStorage(sessionStorageJson);

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

            var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, json);
        }
        catch (Exception ex)
        {
            Report($"Oturum kaydetme hatası: {ex.Message}");
        }
    }

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
            {
                var cookieParts = state.Cookies.Split(';', StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in cookieParts)
                {
                    var partJson = JsonSerializer.Serialize(part.Trim() + "; path=/; domain=.yolcu360.com");
                    await EvaluateScriptAsync($"document.cookie = {partJson};");
                }
            }

            if (state.LocalStorage.Count > 0)
            {
                var localJson = JsonSerializer.Serialize(state.LocalStorage);
                await EvaluateScriptAsync(
                    $$"""
                    (() => {
                        const items = {{localJson}};
                        for (const key in items) {
                            if (Object.prototype.hasOwnProperty.call(items, key)) {
                                localStorage.setItem(key, items[key]);
                            }
                        }
                    })();
                    """);
            }

            if (state.SessionStorage.Count > 0)
            {
                var sessionJson = JsonSerializer.Serialize(state.SessionStorage);
                await EvaluateScriptAsync(
                    $$"""
                    (() => {
                        const items = {{sessionJson}};
                        for (const key in items) {
                            if (Object.prototype.hasOwnProperty.call(items, key)) {
                                sessionStorage.setItem(key, items[key]);
                            }
                        }
                    })();
                    """);
            }

            Report("Kaydedilmiş oturum gömülü tarayıcıya başarıyla restore edildi.");
            return true;
        }
        catch (Exception ex)
        {
            Report($"Oturum yükleme hatası: {ex.Message}");
            return false;
        }
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

namespace Yolcu360Otomasyon.Services;

public sealed partial class BAService
{
    public async Task ClearBrowserSessionAsync()
    {
        Report("Gömülü tarayıcı oturumu ve çerezleri temizleniyor...");
        try
        {
            await NavigateAsync("https://www.yolcu360.com/logout");
            await Task.Delay(LogoutNavigationDelay);

            await EvaluateScriptAsync(
                """
                (() => {
                    try {
                        const domains = ['', '.yolcu360.com', 'www.yolcu360.com', 'yolcu360.com'];
                        const paths = ['/', '/login', '/api'];
                        const cookies = document.cookie.split(";");
                        for (let i = 0; i < cookies.length; i++) {
                            const cookie = cookies[i];
                            const eqPos = cookie.indexOf("=");
                            const name = eqPos > -1 ? cookie.substr(0, eqPos).trim() : cookie.trim();
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
            await Task.Delay(800);
            Report("Gömülü tarayıcı çerezleri ve yerel depolama başarıyla temizlendi.");
        }
        catch (Exception ex)
        {
            Report($"Oturum temizleme hatası: {ex.Message}");
        }
    }
}

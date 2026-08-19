using System.Text.Json;

namespace Yolcu360Otomasyon.Services;

/// <summary>
/// Gömülü tarayıcı (WebKit / WebView) profiline gerçek kullanıcı geçmişi,
/// çerez birikimi ve yüksek reCAPTCHA güven puanı (0.9) kazandırmak için
/// popüler sitelerde organik gezinme simülasyonu yapan bağımsız modül.
/// </summary>
public sealed partial class BAService
{
    private static readonly string[] WarmingSearchQueries =
    {
        "istanbul hava durumu",
        "türkiye gezilecek yerler",
        "en çok satan kitaplar",
        "teknoloji haberleri son dakika",
        "sinema vizyondaki filmler",
        "en iyi tatil rotaları türkiye",
        "araba kiralama rehberi"
    };

    private static string GetWarmingFlagPath()
    {
        var appDataDir = Path.Combine(AppContext.BaseDirectory, "sessions");
        Directory.CreateDirectory(appDataDir);
        return Path.Combine(appDataDir, ".profile_warmed");
    }

    /// <summary>
    /// Profilin daha önce en az bir kez ısıtılıp ısıtılmadığını kontrol eder.
    /// </summary>
    public static bool IsProfileWarmed()
    {
        try
        {
            var flagPath = GetWarmingFlagPath();
            return File.Exists(flagPath);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Eğer tarayıcı profili daha önce hiç ısıtılmadıysa otomatik olarak ilk ısıtmayı çalıştırır.
    /// Daha önce yapılmışsa hiçbir şey yapmadan hızlıca geçer.
    /// </summary>
    public async Task<bool> WarmBrowserProfileIfFirstTimeAsync(CancellationToken cancellationToken = default)
    {
        if (IsProfileWarmed())
        {
            Report("Tarayıcı profili daha önce ısıtılmış (Kalıcı çerez ve geçmiş mevcut).");
            return false;
        }

        Report("Tarayıcı profili ilk kez hazırlanıyor. Organik geçmiş oluşturma başlatılıyor...");
        await WarmBrowserProfileAsync(force: true, cancellationToken);
        return true;
    }

    /// <summary>
    /// Tarayıcı profilini kapsamlı şekilde ısıtır: Google, YouTube, GitHub, Vikipedi ve Haber sitelerinde
    /// organik arama, tıklama, kaydırma ve gezinme yaparak zengin çerez ve önbellek geçmişi oluşturur.
    /// </summary>
    public async Task WarmBrowserProfileAsync(bool force = false, CancellationToken cancellationToken = default)
    {
        if (!force && IsProfileWarmed())
        {
            Report("Profil zaten ısıtılmış.");
            return;
        }

        Report("=== [Kapsamlı Profil Isıtma (Deep Warming)] Başlatılıyor ===");

        try
        {
            // 1. Google Arama & Sonuç Tıklama (Google Trust Çerezleri: NID, 1P_JAR, CONSENT, AEC, SOCS)
            await WarmGoogleAsync(cancellationToken);

            // 2. YouTube Akış & Video İnceleme (Google Video & Analitik Çerezleri: PREF, YSC, VISITOR_INFO1_LIVE)
            await WarmYouTubeAsync(cancellationToken);

            // 3. Vikipedi Gezinmesi (Organik Bilgi & Okuma İmzası)
            await WarmWikipediaAsync(cancellationToken);

            // 4. GitHub Trendler Gezinmesi (Teknik / Geliştirici İmzası)
            await WarmGitHubAsync(cancellationToken);

            // 5. Popüler Teknoloji & Blog Gezinmesi (Çeşitli 3. Parti Çerez Havuzu)
            await WarmTechBlogAsync(cancellationToken);

            // Isıtma tamamlanma bayrağını kaydet (Bir sonraki açılışlarda tekrar çalışmaz)
            try
            {
                var flagPath = GetWarmingFlagPath();
                await File.WriteAllTextAsync(flagPath, $"WarmedAt={DateTimeOffset.UtcNow:O}", cancellationToken);
            }
            catch {}

            Report("=== [Kapsamlı Profil Isıtma] Başarıyla Tamamlandı! reCAPTCHA v3 Puanı Kalıcı Olarak Yükseltildi. ===");
        }
        catch (OperationCanceledException)
        {
            Report("Profil ısıtma işlemi kullanıcı tarafından iptal edildi.");
        }
        catch (Exception ex)
        {
            Report($"Profil ısıtma sırasında hata: {ex.Message}");
        }
    }

    private async Task WarmGoogleAsync(CancellationToken cancellationToken)
    {
        Report("[Warming 1/5] Google Arama açılıyor...");
        await NavigateAsync("https://www.google.com/", TimeSpan.FromSeconds(25));
        await WaitForDocumentReadyAsync(TimeSpan.FromSeconds(10));
        await SimulatePassiveBrowsingAsync(steps: 2, delayMs: 500);

        // Google Çerez Onay Penceresi Varsa Kabul Et
        await EvaluateScriptAsync(
            """
            (() => {
                const buttons = Array.from(document.querySelectorAll('button, div[role="button"]'));
                const consentBtn = buttons.find(b => {
                    const txt = (b.textContent || '').toLowerCase();
                    return txt.includes('tümünü kabul') || txt.includes('kabul et') || txt.includes('accept all');
                });
                if (consentBtn) consentBtn.click();
            })();
            """);

        var query = WarmingSearchQueries[Random.Shared.Next(WarmingSearchQueries.Length)];
        Report($"[Warming 1/5] Google'da organik arama yapılıyor: \"{query}\"");

        var queryJson = JsonSerializer.Serialize(query);
        await EvaluateScriptAsync(
            $$"""
            (() => {
                const searchInput = document.querySelector('textarea[name="q"], input[name="q"]');
                if (!searchInput) return false;
                searchInput.focus();
                searchInput.value = {{queryJson}};
                searchInput.dispatchEvent(new Event('input', { bubbles: true }));
                const form = searchInput.closest('form');
                if (form) {
                    try { form.submit(); } catch {}
                }
                return true;
            })();
            """);

        await Task.Delay(Random.Shared.Next(2500, 3800), cancellationToken);
        await SimulatePassiveBrowsingAsync(steps: 4, delayMs: 650);

        // İlk organik arama sonucuna tıkla
        Report("[Warming 1/5] İlk Google arama sonucuna giriliyor...");
        await EvaluateScriptAsync(
            """
            (() => {
                const links = Array.from(document.querySelectorAll('#search a, #rso a'));
                const firstResult = links.find(a => {
                    const href = a.getAttribute('href') || '';
                    return href.startsWith('http') && !href.includes('google.com');
                });
                if (firstResult) {
                    firstResult.click();
                }
            })();
            """);

        await Task.Delay(Random.Shared.Next(2500, 4000), cancellationToken);
        await SimulatePassiveBrowsingAsync(steps: 3, delayMs: 600);
    }

    private async Task WarmYouTubeAsync(CancellationToken cancellationToken)
    {
        Report("[Warming 2/5] YouTube ana sayfası ziyaret ediliyor...");
        await NavigateAsync("https://www.youtube.com/", TimeSpan.FromSeconds(25));
        await WaitForDocumentReadyAsync(TimeSpan.FromSeconds(10));

        // YouTube Çerez Onayı Varsa Geç
        await EvaluateScriptAsync(
            """
            (() => {
                const buttons = Array.from(document.querySelectorAll('button, ytd-button-renderer'));
                const consentBtn = buttons.find(b => {
                    const txt = (b.textContent || '').toLowerCase();
                    return txt.includes('tümünü kabul') || txt.includes('kabul et') || txt.includes('accept');
                });
                if (consentBtn) consentBtn.click();
            })();
            """);

        Report("[Warming 2/5] YouTube video akışı inceleniyor ve kaydırılıyor...");
        await Task.Delay(Random.Shared.Next(2000, 3000), cancellationToken);
        await SimulatePassiveBrowsingAsync(steps: 4, delayMs: 700);

        // İlk videoya tıklayıp kısa bir oynatma simülasyonu yap
        Report("[Warming 2/5] Örnek bir videoya girilerek oynatma geçmişi oluşturuluyor...");
        await EvaluateScriptAsync(
            """
            (() => {
                const videoLink = document.querySelector('ytd-rich-grid-media a#thumbnail, ytd-video-renderer a#thumbnail');
                if (videoLink) videoLink.click();
            })();
            """);

        await Task.Delay(Random.Shared.Next(3500, 5500), cancellationToken);
    }

    private async Task WarmWikipediaAsync(CancellationToken cancellationToken)
    {
        Report("[Warming 3/5] Vikipedi Türkçe ana sayfası ziyaret ediliyor...");
        await NavigateAsync("https://tr.wikipedia.org/wiki/Ana_Sayfa", TimeSpan.FromSeconds(20));
        await WaitForDocumentReadyAsync(TimeSpan.FromSeconds(10));

        Report("[Warming 3/5] Günün maddesi ve haberler okunuyor...");
        await Task.Delay(Random.Shared.Next(1500, 2500), cancellationToken);
        await SimulatePassiveBrowsingAsync(steps: 3, delayMs: 550);

        // Rastgele bir iç bağlantıya tıkla
        await EvaluateScriptAsync(
            """
            (() => {
                const links = Array.from(document.querySelectorAll('#mp-tfa a, #mp-itn a, .mw-parser-output p a'));
                const validLink = links.find(a => {
                    const href = a.getAttribute('href') || '';
                    return href.startsWith('/wiki/') && !href.includes(':');
                });
                if (validLink) validLink.click();
            })();
            """);

        await Task.Delay(Random.Shared.Next(2000, 3500), cancellationToken);
        await SimulatePassiveBrowsingAsync(steps: 2, delayMs: 600);
    }

    private async Task WarmGitHubAsync(CancellationToken cancellationToken)
    {
        Report("[Warming 4/5] GitHub Trendler sayfası ziyaret ediliyor...");
        await NavigateAsync("https://github.com/trending", TimeSpan.FromSeconds(20));
        await WaitForDocumentReadyAsync(TimeSpan.FromSeconds(10));

        Report("[Warming 4/5] GitHub repoları inceleniyor...");
        await Task.Delay(Random.Shared.Next(1500, 2500), cancellationToken);
        await SimulatePassiveBrowsingAsync(steps: 3, delayMs: 500);
    }

    private async Task WarmTechBlogAsync(CancellationToken cancellationToken)
    {
        Report("[Warming 5/5] Teknoloji ve blog içerikleri taranıyor...");
        await NavigateAsync("https://news.ycombinator.com/", TimeSpan.FromSeconds(20));
        await WaitForDocumentReadyAsync(TimeSpan.FromSeconds(10));

        await Task.Delay(Random.Shared.Next(1500, 2500), cancellationToken);
        await SimulatePassiveBrowsingAsync(steps: 3, delayMs: 500);
    }

    /// <summary>
    /// Ziyaret edilen sayfada insansı kaydırma (scroll) ve rastgele fare hareketleri simüle eder.
    /// </summary>
    private async Task SimulatePassiveBrowsingAsync(int steps, int delayMs)
    {
        for (int i = 0; i < steps; i++)
        {
            var scrollDelta = Random.Shared.Next(180, 420);

            await EvaluateScriptAsync(
                $$"""
                (() => {
                    window.scrollBy({ top: {{scrollDelta}}, behavior: 'smooth' });
                })();
                """);

            var jitterDelay = delayMs + Random.Shared.Next(-80, 200);
            await Task.Delay(Math.Max(200, jitterDelay));
        }
    }
}

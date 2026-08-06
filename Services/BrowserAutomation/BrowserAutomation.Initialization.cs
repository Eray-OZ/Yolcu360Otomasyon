using PuppeteerSharp;

namespace Yolcu360Otomasyon.Services;

public sealed partial class BrowserAutomationService
{
    public async Task InitializeAsync(bool headless = true, bool restoreSession = true)
    {
        if (_browser is not null && _page is not null)
            return;

        PrepareChromeProfileSnapshot();
        ClearChromeSingletonArtifacts();

        var executablePath = File.Exists(ChromeExecutablePath) ? ChromeExecutablePath : null;
        if (executablePath is null)
            await new BrowserFetcher().DownloadAsync();

        _browser = await Puppeteer.LaunchAsync(new LaunchOptions
        {
            Headless = headless,
            ExecutablePath = executablePath,
            UserDataDir = ChromeUserDataDir,
            Args =
            [
                "--no-sandbox",
                "--disable-setuid-sandbox",
                "--no-first-run",
                "--no-default-browser-check",
                "--disable-default-apps",
                "--disable-blink-features=AutomationControlled",
                "--disable-features=IsolateOrigins,site-per-process",
                $"--profile-directory={ChromeProfileDirectory}",
                "--flag-switches-begin",
                "--disable-site-isolation-trials",
                "--flag-switches-end",
            ],
            DefaultViewport = new ViewPortOptions
            {
                Width = 1440,
                Height = 900
            }
        });

        var pages = await _browser.PagesAsync();
        _page = pages.FirstOrDefault(page =>
            !page.Url.StartsWith("chrome-extension://", StringComparison.OrdinalIgnoreCase))
            ?? await _browser.NewPageAsync();

        await _page.BringToFrontAsync();
        await _page.SetUserAgentAsync("Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/26.5.2 Safari/605.1.15");

        await _page.EvaluateFunctionOnNewDocumentAsync(@"() => {
    Object.defineProperty(navigator, 'webdriver', {
        get: () => undefined
    });
    window.chrome = { runtime: {} };
    Object.defineProperty(navigator, 'languages', {
        get: () => ['tr-TR', 'tr', 'en-US', 'en']
    });
}");

        if (restoreSession)
            await TryRestoreSessionAsync();
    }

    private static void PrepareChromeProfileSnapshot()
    {
        Directory.CreateDirectory(ChromeUserDataDir);

        var sourceLocalState = Path.Combine(ChromeSourceUserDataDir, "Local State");
        var targetLocalState = Path.Combine(ChromeUserDataDir, "Local State");
        if (File.Exists(sourceLocalState))
            File.Copy(sourceLocalState, targetLocalState, overwrite: true);

        var sourceDefaultProfile = Path.Combine(ChromeSourceUserDataDir, ChromeProfileDirectory);
        var targetDefaultProfile = Path.Combine(ChromeUserDataDir, ChromeProfileDirectory);

        if (Directory.Exists(targetDefaultProfile))
            Directory.Delete(targetDefaultProfile, recursive: true);

        CopyDirectory(sourceDefaultProfile, targetDefaultProfile);
    }

    private static void ClearChromeSingletonArtifacts()
    {
        var paths = new[]
        {
            Path.Combine(ChromeUserDataDir, "SingletonLock"),
            Path.Combine(ChromeUserDataDir, "SingletonSocket"),
            Path.Combine(ChromeUserDataDir, "SingletonCookie"),
            Path.Combine(ChromeUserDataDir, ChromeProfileDirectory, "SingletonLock"),
            Path.Combine(ChromeUserDataDir, ChromeProfileDirectory, "SingletonSocket"),
            Path.Combine(ChromeUserDataDir, ChromeProfileDirectory, "SingletonCookie")
        };

        foreach (var path in paths)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                    continue;
                }

                if (Directory.Exists(path))
                    Directory.Delete(path, recursive: true);
            }
            catch
            {
            }
        }
    }

    private static void CopyDirectory(string sourceDir, string targetDir)
    {
        if (!Directory.Exists(sourceDir))
            throw new DirectoryNotFoundException($"Chrome profil klasörü bulunamadı: {sourceDir}");

        Directory.CreateDirectory(targetDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var fileName = Path.GetFileName(file);
            var targetFile = Path.Combine(targetDir, fileName);
            File.Copy(file, targetFile, overwrite: true);
        }

        foreach (var directory in Directory.GetDirectories(sourceDir))
        {
            var directoryName = Path.GetFileName(directory);
            var targetSubDirectory = Path.Combine(targetDir, directoryName);
            CopyDirectory(directory, targetSubDirectory);
        }
    }
}

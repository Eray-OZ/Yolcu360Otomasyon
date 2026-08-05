using System.Text.Json;
using System.Text.Json.Serialization;
using PuppeteerSharp;
using Yolcu360Otomasyon.Models;

namespace Yolcu360Otomasyon.Services;

public sealed class BrowserAutomationService : IAsyncDisposable
{
    private const string Yolcu360HomeUrl = "https://www.yolcu360.com/";
    private const string SessionStateFilePath = "/Users/erayoz/Codes/Staj/Yolcu360Otomasyon/session_state.json";

    private IBrowser? _browser;
    private IPage? _page;

    public event Action<string>? ProgressChanged;

    // ─── Selector Sabitleri ───────────────────────────────────────────────────
    // Yolcu360 sayfa yapısı değişirse ilk olarak bu bölüm güncellenir.
    private static class Selectors
    {
        // Login
        public const string LoginPagePhoneInput = "#phn-input";
        public const string LoginPageContinueButton = "button";

        // Arama formu — canlı HTML'den doğrulanmış
        public const string PickupLocationInput = "#inputPickUpLocation";

        // Takvim (VueDatePicker): İlk .dp__main → alış tarihi, ikincisi → bırakış tarihi
        public const string AllDatePickers      = ".dp__main.dp__theme_light";

        // Alış tarihi container (modaltitlecmskey'i olan kapsayıcı)
        public const string PickupDateContainer  = "[modaltitlecmskey='pickup_and_dropoff_date'] .dp__main.dp__theme_light";
        // Bırakış tarihi container (cmskey olmayan ikinci group)
        // JS ile index=1 olarak seçilecek

        // Takvim menüsü (açıldıktan sonra)
        public const string DatePickerMenu      = ".dp__menu";
        public const string DatePickerNextMonth = ".dp__nav_btn[data-dp-element='action-next'], .dp__next_btn, button[aria-label*='Next']";
        public const string DatePickerPrevMonth = ".dp__nav_btn[data-dp-element='action-prev'], .dp__prev_btn, button[aria-label*='Prev']";
        public const string DatePickerMonthYear = ".dp__month_year_select, .dp__calendar_header_item--current, .dp__action_select";

        // Arama butonu — id="search", data-cms-key="search"
        public const string SearchButton = "#search";

        // Filtreler (arama sonuçları sayfası)
        public const string AutomaticTransmissionFilter = "[data-filter='automatic']";
        public const string ManualTransmissionFilter    = "[data-filter='manual']";
        public const string DieselFuelFilter            = "[data-filter='diesel']";
        public const string GasolineFuelFilter          = "[data-filter='gasoline']";
    }

    // ─── Başlatma ─────────────────────────────────────────────────────────────

    public async Task InitializeAsync(bool headless = true, bool restoreSession = true)
    {
        if (_browser is not null && _page is not null)
            return;

        await new BrowserFetcher().DownloadAsync();

        _browser = await Puppeteer.LaunchAsync(new LaunchOptions
        {
            Headless = headless,
            Args = ["--no-sandbox", "--disable-setuid-sandbox"]
        });

        _page = await _browser.NewPageAsync();

        await _page.SetViewportAsync(new ViewPortOptions
        {
            Width  = 1440,
            Height = 900
        });

        if (restoreSession)
            await TryRestoreSessionAsync();
    }

    // ─── Login ────────────────────────────────────────────────────────────────

    public async Task LoginWithPhoneAsync(string phoneNumber)
    {
        var page = GetPage();

        if (string.IsNullOrWhiteSpace(phoneNumber))
            throw new InvalidOperationException("Telefon numarası boş bırakılamaz.");

        await page.GoToAsync("https://www.yolcu360.com/login?redirect=%2F", WaitUntilNavigation.Networkidle2);
        await CloseInitialPopupAsync();

        await page.WaitForSelectorAsync(Selectors.LoginPagePhoneInput, new WaitForSelectorOptions
        {
            Visible = true,
            Timeout = 30_000
        });

        var normalizedPhone = NormalizePhoneNumber(phoneNumber);
        await NativeSetInputAsync(Selectors.LoginPagePhoneInput, normalizedPhone);

        var continueClicked = await page.EvaluateExpressionAsync<bool>(
            """
            (() => {
                const buttons = Array.from(document.querySelectorAll('button'));
                const button = buttons.find(current => (current.textContent || '').replace(/\s+/g, ' ').trim() === 'Devam Et');
                if (!button) return false;
                button.click();
                return true;
            })();
            """);

        if (!continueClicked)
            throw new InvalidOperationException("Login sayfasında 'Devam Et' butonu bulunamadı.");

        await WaitAsync(2_000);
    }

    public async Task<bool> IsSmsVerificationRequiredAsync()
    {
        var page = GetPage();

        try
        {
            return await page.EvaluateExpressionAsync<bool>(
                """
                (() => {
                    const text = document.body.innerText.toLocaleLowerCase('tr-TR');
                    const otpInputs = Array.from(document.querySelectorAll('input'))
                        .filter(input => {
                            const rect = input.getBoundingClientRect();
                            const style = window.getComputedStyle(input);
                            if (rect.width <= 0 || rect.height <= 0) return false;
                            if (style.display === 'none' || style.visibility === 'hidden') return false;

                            const attrs = `${input.id} ${input.name} ${input.placeholder} ${input.autocomplete} ${input.inputMode} ${input.type}`.toLocaleLowerCase('tr-TR');
                            return attrs.includes('otp')
                                || attrs.includes('code')
                                || attrs.includes('kod')
                                || input.maxLength === 1;
                        });

                    return otpInputs.length > 0
                        || text.includes('doğrulama kodu')
                        || text.includes('sms doğrulama')
                        || text.includes('tek kullanımlık')
                        || text.includes('verification code');
                })();
                """);
        }
        catch
        {
            return false;
        }
    }

    public async Task FillSmsVerificationCodeAsync(string code)
    {
        var page = GetPage();

        if (string.IsNullOrWhiteSpace(code))
            throw new InvalidOperationException("SMS doğrulama kodu boş.");

        Report($"SMS kodu giriliyor: {code}");

        try
        {
            await page.WaitForFunctionAsync(
                """
                () => {
                    const visible = el => {
                        const rect = el.getBoundingClientRect();
                        const style = window.getComputedStyle(el);
                        return rect.width > 0 &&
                            rect.height > 0 &&
                            style.display !== 'none' &&
                            style.visibility !== 'hidden';
                    };

                    const text = (document.body.innerText || '').toLocaleLowerCase('tr-TR');
                    const inputs = Array.from(document.querySelectorAll('input, [contenteditable="true"]')).filter(visible);
                    return inputs.length > 0
                        || text.includes('doğrulama kodu')
                        || text.includes('sms doğrulama')
                        || text.includes('telefonunuza')
                        || text.includes('6 haneli');
                }
                """,
                new WaitForFunctionOptions { Timeout = 30_000 });
        }
        catch (WaitTaskTimeoutException)
        {
            throw new InvalidOperationException("SMS doğrulama ekranı zamanında açılmadı.");
        }

        await WaitAsync(1_000);

        var filled = await page.EvaluateFunctionAsync<bool>(
            """
            (code) => {
                const normalize = value => (value || '').toLocaleLowerCase('tr-TR');
                const visible = el => {
                    const rect = el.getBoundingClientRect();
                    const style = window.getComputedStyle(el);
                    return rect.width > 0 &&
                        rect.height > 0 &&
                        style.display !== 'none' &&
                        style.visibility !== 'hidden';
                };

                const setValue = (el, value) => {
                    const prototype = el instanceof HTMLInputElement || el instanceof HTMLTextAreaElement
                        ? Object.getPrototypeOf(el)
                        : null;
                    const descriptor = prototype ? Object.getOwnPropertyDescriptor(prototype, 'value') : null;
                    if (descriptor?.set) {
                        descriptor.set.call(el, value);
                    } else if ('value' in el) {
                        el.value = value;
                    } else {
                        el.textContent = value;
                    }
                };

                const dispatchInput = el => {
                    el.dispatchEvent(new KeyboardEvent('keydown', { bubbles: true }));
                    el.dispatchEvent(new Event('input', { bubbles: true }));
                    el.dispatchEvent(new Event('change', { bubbles: true }));
                    el.dispatchEvent(new KeyboardEvent('keyup', { bubbles: true }));
                    el.dispatchEvent(new KeyboardEvent('keypress', { bubbles: true }));
                    el.dispatchEvent(new Event('blur', { bubbles: true }));
                };

                const allInputs = Array.from(document.querySelectorAll('input, [contenteditable="true"]')).filter(visible);
                const otpLikeInputs = allInputs.filter(input => {
                    const attrs = normalize(`${input.id} ${input.name} ${input.placeholder} ${input.autocomplete} ${input.inputMode} ${input.type} ${input.className} ${input.getAttribute?.('aria-label') || ''}`);
                    return attrs.includes('otp')
                        || attrs.includes('code')
                        || attrs.includes('kod')
                        || attrs.includes('pin')
                        || attrs.includes('verify')
                        || attrs.includes('dogrulama')
                        || attrs.includes('sms')
                        || input.maxLength === 1;
                });

                const singleCharInputs = otpLikeInputs.filter(input => input.maxLength === 1);
                if (singleCharInputs.length >= code.length)
                {
                    singleCharInputs.slice(0, code.length).forEach((input, index) => {
                        input.focus();
                        input.click?.();
                        setValue(input, code[index]);
                        dispatchInput(input);
                    });
                    return true;
                }

                const singleInput = otpLikeInputs.find(input => input.maxLength !== 1)
                    || allInputs.find(input => {
                        const attrs = normalize(`${input.id} ${input.name} ${input.placeholder} ${input.autocomplete} ${input.inputMode} ${input.type} ${input.className} ${input.getAttribute?.('aria-label') || ''}`);
                        return attrs.includes('otp')
                            || attrs.includes('code')
                            || attrs.includes('kod')
                            || attrs.includes('pin')
                            || attrs.includes('verify')
                            || attrs.includes('dogrulama')
                            || attrs.includes('sms');
                    });

                if (singleInput)
                {
                    singleInput.focus();
                    singleInput.click?.();
                    setValue(singleInput, code);
                    dispatchInput(singleInput);
                    return true;
                }

                const fallbackInput = allInputs.find(input => {
                    const type = normalize(input.type);
                    return type === 'tel' || type === 'text' || type === 'number';
                });

                if (fallbackInput)
                {
                    fallbackInput.focus();
                    fallbackInput.click?.();
                    setValue(fallbackInput, code);
                    dispatchInput(fallbackInput);
                    return true;
                }

                return false;
            }
            """,
            code);

        if (!filled)
            throw new InvalidOperationException("SMS doğrulama alanı bulunamadı.");

        await WaitAsync(500);

        try
        {
            var clicked = await page.EvaluateExpressionAsync<bool>(
                """
                (() => {
                    const normalize = value => (value || '').replace(/\s+/g, ' ').trim().toLocaleLowerCase('tr-TR');
                    const visible = el => {
                        const rect = el.getBoundingClientRect();
                        const style = window.getComputedStyle(el);
                        return rect.width > 0 &&
                            rect.height > 0 &&
                            style.display !== 'none' &&
                            style.visibility !== 'hidden' &&
                            !el.disabled;
                    };

                    const exactTexts = ['doğrula', 'onayla', 'devam et', 'giriş yap', 'verify', 'continue'];
                    const partialTexts = ['doğrula', 'onay', 'devam', 'verify', 'continue'];

                    const candidates = Array.from(document.querySelectorAll('button, [role="button"], input[type="submit"], input[type="button"]'))
                        .filter(visible)
                        .map(element => {
                            const text = normalize(element.textContent || element.value || element.getAttribute('aria-label') || element.getAttribute('title') || '');
                            return { element, text };
                        })
                        .filter(item => item.text.length > 0);

                    const exactMatch = candidates.find(item => exactTexts.includes(item.text));
                    if (exactMatch) {
                        exactMatch.element.click();
                        return true;
                    }

                    const partialMatch = candidates.find(item => partialTexts.some(text => item.text.includes(text)));
                    if (partialMatch) {
                        partialMatch.element.click();
                        return true;
                    }

                    return false;
                })();
                """);

            Report(clicked
                ? "SMS doğrulama butonu tıklandı."
                : "SMS doğrulama butonu bulunamadı.");
        }
        catch
        {
            // Doğrulama butonu yoksa alan doldurulmuş olarak bırakılır.
        }
    }

    // ─── Ana Arama Akışı ──────────────────────────────────────────────────────

    public async Task ApplySearchFiltersAndSearchAsync(SearchFilter filter)
    {
        var page = GetPage();

        // 1. Anasayfayı aç
        Report("Yolcu360 ana sayfası açılıyor...");
        await page.GoToAsync(Yolcu360HomeUrl, WaitUntilNavigation.Networkidle2);
        await ShowDebugAsync("Sayfa açıldı.");

        // 2. Nuxt hydration ısınma hareketi
        Report("Sayfa etkileşime hazırlanıyor...");
        await WarmUpHydrationAsync();
        Report("Başlangıç popup'ı için bekleniyor...");
        await WaitAsync(10_000);
        await CloseInitialPopupAsync();

        // 3. Alış yeri
        Report($"Alış yeri yazılıyor: {filter.PickupLocation}");
        await FillPickupLocationAsync(filter.PickupLocation);

        // 4 + 6. Alış ve Bırakış tarihleri — tek range picker'da birlikte seç
        Report($"Tarihler seçiliyor: {filter.PickupDate:dd.MM.yyyy} – {filter.ReturnDate:dd.MM.yyyy}");
        await SelectDateRangeAsync(filter.PickupDate, filter.ReturnDate);

        // 5. Alış saati
        Report($"Alış saati seçiliyor: {filter.PickupTime}");
        await SelectTimeAsync(timePickerIndex: 0, filter.PickupTime);

        // 7. Bırakış saati
        Report($"Bırakış saati seçiliyor: {filter.ReturnTime}");
        await SelectTimeAsync(timePickerIndex: 1, filter.ReturnTime);

        // 8. İsteğe bağlı filtreler
        await ClickOptionalFilterAsync(GetTransmissionSelector(filter.TransmissionType));
        await ClickOptionalFilterAsync(GetFuelSelector(filter.FuelType));

        // 9. Arama
        Report("Araç Ara butonuna tıklanıyor...");
        await ClickSearchButtonAsync();

        Report("Sonuç ekranı bekleniyor...");
        await WaitForSearchResultAsync();
    }

    public async Task<IReadOnlyList<SearchResultItem>> ReadSearchResultsAsync()
    {
        var page = GetPage();

        Report("Sonuç kartları bekleniyor...");

        try
        {
            await page.WaitForFunctionAsync(
                """
                () => {
                    const cards = document.querySelectorAll('#car_card_list .car-card, .car-card');
                    return cards.length > 0;
                }
                """,
                new WaitForFunctionOptions { Timeout = 30_000 });
        }
        catch (WaitTaskTimeoutException)
        {
            var diag = await GetDiagnosticAsync();
            throw new InvalidOperationException($"Sonuç kartları yüklenmedi. {diag}");
        }

        await WaitAsync(2_000);

        Report("Sonuçlar okunuyor...");

        var results = await page.EvaluateFunctionAsync<SearchResultItem[]>(
            """
            () => {
                const normalize = value => (value || '').replace(/\s+/g, ' ').trim();

                return Array.from(document.querySelectorAll('#car_card_list .car-card, .car-card'))
                    .map(card => {
                        const specs = Array.from(card.querySelectorAll('.icon-gear-type, .icon-gas-type'))
                            .map(icon => normalize(icon.parentElement?.textContent))
                            .filter(Boolean);

                        const title = normalize(card.querySelector('.text-dark-gray.text-lg.font-bold')?.textContent);
                        const subtitle = normalize(card.querySelector('[data-cms-key="or_similar"]')?.textContent);
                        const price = normalize(card.querySelector('#car_total_price')?.textContent);
                        const dailyPrice = normalize(card.querySelector('[data-cms-key="text_daily_price2"]')?.textContent);
                        const transmission = specs.find(text => /manuel|otomatik/i.test(text)) || '';
                        const fuelType = specs.find(text => /benzin|dizel|hibrit|elektrik/i.test(text)) || '';
                        const supplier = normalize(card.querySelector('figure img[alt]')?.getAttribute('alt'));
                        const pickupInfo = normalize(card.querySelector('.icon-filled')?.parentElement?.textContent);
                        const actionText = normalize(card.querySelector('[data-cms-key="button_rent_now"]')?.textContent)
                            || normalize(card.querySelector('button')?.textContent);
                        const url = normalize(card.querySelector('a[href]')?.getAttribute('href'));

                        return {
                            title,
                            subtitle,
                            price,
                            dailyPrice,
                            transmission,
                            fuelType,
                            supplier,
                            pickupInfo,
                            actionText,
                            url
                        };
                    })
                    .filter(item => item.title || item.price);
            }
            """);

        Report($"{results.Length} sonuç okundu.");
        return results;
    }

    // ─── Alış Yeri ────────────────────────────────────────────────────────────

    private async Task FillPickupLocationAsync(string location)
    {
        var page = GetPage();

        if (string.IsNullOrWhiteSpace(location))
            throw new InvalidOperationException("Alış yeri boş bırakılamaz.");

        await page.WaitForSelectorAsync(Selectors.PickupLocationInput, new WaitForSelectorOptions
        {
            Visible  = true,
            Timeout  = 30_000
        });

        // Odaklan ve temizle
        await page.FocusAsync(Selectors.PickupLocationInput);
        await page.EvaluateExpressionAsync("""
            (() => {
                const el = document.querySelector('#inputPickUpLocation');
                el.focus();
                el.select();
            })();
            """);

        // Yazma sırasında form submit / beforeunload ile sayfanın yenilenmesini engelle.
        // Guard, seçim doğrulanana kadar açık kalır.
        await page.EvaluateExpressionAsync("""
            (() => {
                document.querySelectorAll('form').forEach(f => {
                    f.__y360_submitGuard = (e) => { e.preventDefault(); e.stopImmediatePropagation(); };
                    f.addEventListener('submit', f.__y360_submitGuard, true);
                });
                window.__y360_keyGuard = (e) => {
                    if (document.activeElement?.id === 'inputPickUpLocation' && e.key === 'Enter') {
                        e.preventDefault();
                        e.stopImmediatePropagation();
                    }
                };
                window.addEventListener('keydown', window.__y360_keyGuard, true);
            })();
            """);

        try
        {
            await page.Keyboard.TypeAsync(location, new PuppeteerSharp.Input.TypeOptions { Delay = 80 });

            // Eğer yazma sırasında navigation başladıysa geri dön ve tekrar bekle
            if (!page.Url.Contains("yolcu360.com") || page.Url.Contains("search") || page.Url.Contains("arac-kiralama"))
            {
                await ShowDebugAsync("Sayfa yenilendi, anasayfaya dönülüyor...");
                await page.GoToAsync(Yolcu360HomeUrl, WaitUntilNavigation.Networkidle2);
                await WarmUpHydrationAsync();
                await WaitAsync(10_000);
                await CloseInitialPopupAsync();
                await page.WaitForSelectorAsync(Selectors.PickupLocationInput, new WaitForSelectorOptions
                {
                    Visible = true,
                    Timeout = 30_000
                });
                await page.FocusAsync(Selectors.PickupLocationInput);
                await page.Keyboard.TypeAsync(location, new PuppeteerSharp.Input.TypeOptions { Delay = 80 });
            }

            // Autocomplete açılmasını bekle
            try
            {
                await page.WaitForFunctionAsync(
                    """
                    () => {
                        const items = Array.from(document.querySelectorAll('.search-autocomplete .location-item'));
                        return items.some(el => {
                            const rect = el.getBoundingClientRect();
                            const style = window.getComputedStyle(el);
                            return rect.width > 0 &&
                                rect.height > 0 &&
                                style.display !== 'none' &&
                                style.visibility !== 'hidden';
                        });
                    }
                    """,
                    new WaitForFunctionOptions { Timeout = 8_000 });
            }
            catch (WaitTaskTimeoutException)
            {
                await ShowDebugAsync("Alış yeri menüsü görünmedi, genel fallback deneniyor.");
                await WaitAsync(1_500);
            }

            var selectionApplied = false;

            for (var attempt = 1; attempt <= 3 && !selectionApplied; attempt++)
            {
                if (attempt > 1)
                {
                    await ShowDebugAsync($"Alış yeri click seçimi tekrar deneniyor. Deneme: {attempt}");
                    await page.FocusAsync(Selectors.PickupLocationInput);
                    await WaitAsync(400);
                }

                var locationJson = JsonSerializer.Serialize(location);
                var suggestionPoint = await page.EvaluateExpressionAsync<ClickPoint>($$"""
                    (() => {
                        const input = document.querySelector('#inputPickUpLocation');
                        if (!input) {
                            return { found: false, enabled: false, x: 0, y: 0, text: 'input yok' };
                        }

                        const inputRect = input.getBoundingClientRect();
                        const visible = el => {
                            const rect = el.getBoundingClientRect();
                            const style = window.getComputedStyle(el);
                            return rect.width > 0 &&
                                rect.height > 0 &&
                                style.display !== 'none' &&
                                style.visibility !== 'hidden';
                        };

                        const normalize = text => text
                            .toLocaleLowerCase('tr-TR')
                            .replace(/\s+/g, ' ')
                            .trim();

                        const locationText = normalize({{locationJson}});

                        const allItems = Array.from(document.querySelectorAll('.search-autocomplete .location-item'));
                        const candidates = allItems
                            .filter(el => {
                                if (!visible(el)) return false;
                                if (el === input || el.contains(input)) return false;

                                const rect = el.getBoundingClientRect();
                                const text = (el.textContent || '').trim();

                                return text.length > 1 &&
                                    rect.top >= inputRect.bottom - 8 &&
                                    rect.left < inputRect.right + 500 &&
                                    rect.right > inputRect.left;
                            })
                            .sort((a, b) => {
                                const aText = normalize(a.textContent || '');
                                const bText = normalize(b.textContent || '');
                                const aScore = aText === locationText ? 0 : aText.startsWith(locationText) ? 1 : aText.includes(locationText) ? 2 : 3;
                                const bScore = bText === locationText ? 0 : bText.startsWith(locationText) ? 1 : bText.includes(locationText) ? 2 : 3;
                                if (aScore !== bScore) return aScore - bScore;
                                const ar = a.getBoundingClientRect();
                                const br = b.getBoundingClientRect();
                                return ar.top === br.top ? ar.left - br.left : ar.top - br.top;
                            });

                        const target = candidates[0];
                        if (!target) {
                            return { found: false, enabled: false, x: 0, y: 0, text: 'öneri bulunamadı' };
                        }

                        target.scrollIntoView({ block: 'center', inline: 'nearest' });
                        const rect = target.getBoundingClientRect();

                        return {
                            found: true,
                            enabled: true,
                            x: rect.left + rect.width / 2,
                            y: rect.top + rect.height / 2,
                            text: (target.textContent || '').trim(),
                            index: allItems.indexOf(target)
                        };
                    })();
                    """);

                if (!suggestionPoint.Found)
                    throw new InvalidOperationException(
                        $"Alış yeri önerisi bulunamadı. Yazılan değer: {location}");

                await WaitAsync(900);
                await page.Mouse.MoveAsync(suggestionPoint.X, suggestionPoint.Y);
                await WaitAsync(150);
                await page.Mouse.ClickAsync(suggestionPoint.X, suggestionPoint.Y);

                await WaitAsync(700);

                try
                {
                    await page.WaitForFunctionAsync(
                        """
                        () => {
                            const input = document.querySelector('#inputPickUpLocation');
                            const menu = document.querySelector('.search-autocomplete');
                            const menuVisible = !!menu && menu.getBoundingClientRect().height > 0;
                            return !!input && input.value.trim().length > 0 && !menuVisible;
                        }
                        """,
                        new WaitForFunctionOptions { Timeout = 2_000 });
                    selectionApplied = true;
                }
                catch (WaitTaskTimeoutException)
                {
                    await ShowDebugAsync("Öneri click sonrası seçim henüz uygulanmadı.");
                }
            }

            if (!selectionApplied)
            {
                throw new InvalidOperationException(
                    $"Alış yeri önerisi seçilemedi. Yazılan değer: {location}");
            }
        }
        finally
        {
            // Guard'ı seçim tamamlandıktan sonra kaldır.
            await page.EvaluateExpressionAsync("""
                (() => {
                    document.querySelectorAll('form').forEach(f => {
                        if (f.__y360_submitGuard) {
                            f.removeEventListener('submit', f.__y360_submitGuard, true);
                            delete f.__y360_submitGuard;
                        }
                    });
                    if (window.__y360_keyGuard) {
                        window.removeEventListener('keydown', window.__y360_keyGuard, true);
                        delete window.__y360_keyGuard;
                    }
                })();
                """);
        }

        // Seçimin kabul edilip edilmediğini doğrula
        try
        {
            await page.WaitForFunctionAsync(
                """
                () => {
                    const input = document.querySelector('#inputPickUpLocation');
                    const menu = document.querySelector('.search-autocomplete');
                    const menuVisible = !!menu && menu.getBoundingClientRect().height > 0;
                    return !!input && input.value.trim().length > 0 && !menuVisible;
                }
                """,
                new WaitForFunctionOptions { Timeout = 3_000 });
        }
        catch (WaitTaskTimeoutException)
        {
            await ShowDebugAsync("Alış yeri menüsü kapanmadı; input değeri kontrol ediliyor.");
        }

        await WaitAsync(800);
        var value = await page.EvaluateExpressionAsync<string>(
            "document.querySelector('#inputPickUpLocation')?.value || ''");

        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException(
                $"Alış yeri '{location}' girilemedi. Autocomplete listesinden geçerli bir konum seçilmesi gerekiyor.");

        Report($"Alış yeri seçildi: {value}");
        await ShowDebugAsync($"Alış yeri: {value}");

        // Autocomplete/listbox açık kalırsa sonraki tarih tıklamasını engelleyebilir.
        await page.Keyboard.PressAsync("Escape");
        await page.EvaluateExpressionAsync("document.activeElement?.blur();");
        await WaitAsync(300);
    }

    // ─── Tarih Seçimi (VueDatePicker) ────────────────────────────────────────

    /// <summary>
    /// Range picker'da alış ve bırakış tarihlerini tek seferde seçer.
    /// Yolcu360, iki tarihi tek bir VueDatePicker range bileşeninde gösteriyor;
    /// picker ikinci kez açılınca ilk seçim sıfırlanıyor. Bu metot takvimi
    /// yalnızca bir kez açıp ardışık iki tarihi seçer.
    /// </summary>
    private async Task SelectDateRangeAsync(DateTime pickupDate, DateTime returnDate)
    {
        var page = GetPage();

        // Alış tarihi picker'a tıkla (range picker'ı açar)
        var pickerPoint = await page.EvaluateExpressionAsync<ClickPoint>("""
            (() => {
                const labelEl = Array.from(document.querySelectorAll('span, div'))
                    .find(el => el.textContent.trim() === 'Alış Tarihi');
                const picker = labelEl?.closest('.dp__main.dp__theme_light');
                if (!picker) return { found: false, enabled: false, x: 0, y: 0, text: '' };
                picker.scrollIntoView({ block: 'center', inline: 'center' });
                const rect = picker.getBoundingClientRect();
                return { found: true, enabled: rect.width > 0 && rect.height > 0,
                         x: rect.left + rect.width / 2, y: rect.top + rect.height / 2, text: '' };
            })();
            """);

        if (!pickerPoint.Found || !pickerPoint.Enabled)
            throw new InvalidOperationException("Alış Tarihi picker bulunamadı.");

        await page.Mouse.ClickAsync(pickerPoint.X, pickerPoint.Y);

        // Takvim mensünün açılmasını bekle
        await page.WaitForSelectorAsync(Selectors.DatePickerMenu, new WaitForSelectorOptions
        {
            Visible = true,
            Timeout = 10_000
        });
        await ShowDebugAsync($"Takvim açıldı. Hedef: {pickupDate:dd.MM.yyyy} – {returnDate:dd.MM.yyyy}");

        // Alış tarihi için doğru aya git
        await NavigateToMonthAsync(pickupDate);

        // Alış gününü seç (range başlatılır, takvim kapanmaz)
        var pickupSelected = await ClickCalendarDayAsync(pickupDate);
        if (!pickupSelected)
            throw new InvalidOperationException($"Alış tarihi {pickupDate:dd.MM.yyyy} seçilemedi.");

        Report($"Alış tarihi seçildi: {pickupDate:dd.MM.yyyy}");
        await ShowDebugAsync($"Alış tarihi seçildi: {pickupDate:dd.MM.yyyy}");

        // Takvim hâlâ açık — range'in ikinci parçası (bırakış) için bekliyoruz.
        await WaitAsync(400);

        // Eğer bırakış tarihi farklı bir ayda ise ona navige et
        if (returnDate.Year != pickupDate.Year || returnDate.Month != pickupDate.Month)
        {
            await NavigateToMonthAsync(returnDate);
            await WaitAsync(300);
        }

        // Bırakış gününü seç (range tamamlanır, takvim kapanır)
        var returnSelected = await ClickCalendarDayAsync(returnDate);
        if (!returnSelected)
            throw new InvalidOperationException($"Bırakış tarihi {returnDate:dd.MM.yyyy} seçilemedi.");

        Report($"Bırakış tarihi seçildi: {returnDate:dd.MM.yyyy}");
        await ShowDebugAsync($"Bırakış tarihi seçildi: {returnDate:dd.MM.yyyy}");
        await WaitAsync(500);
    }

    /// <summary>
    /// Açık .dp__menu içinde hedef tarihin gününü bulup tıklar.
    /// </summary>
    private async Task<bool> ClickCalendarDayAsync(DateTime date)
    {
        var page      = GetPage();
        var dayJson   = JsonSerializer.Serialize(date.Day);
        var monthJson = JsonSerializer.Serialize(
            new[] { "Ocak","Şubat","Mart","Nisan","Mayıs","Haziran",
                    "Temmuz","Ağustos","Eylül","Ekim","Kasım","Aralık" }[date.Month - 1]);
        var yearJson  = JsonSerializer.Serialize(date.Year.ToString());

        return await page.EvaluateExpressionAsync<bool>($$"""
            (() => {
                const menu = Array.from(document.querySelectorAll('.dp__menu'))
                    .find(m => window.getComputedStyle(m).display !== 'none' && m.getBoundingClientRect().width > 0);
                if (!menu) return false;

                const dayTarget   = {{dayJson}};
                const monthTarget = {{monthJson}};
                const yearTarget  = {{yearJson}};

                // İki aylı gösterimde hedef aya ait .dp__calendar panel ini bul.
                const allCalendars = Array.from(menu.querySelectorAll('.dp__calendar'));
                let searchRoot = allCalendars.length > 0 ? null : menu;

                for (const cal of allCalendars) {
                    // Panel başlığında ay ismini ara
                    const hdr = cal.querySelector('.dp__month_year_select');
                    const hdrText = hdr?.textContent?.trim() ?? '';
                    if (hdrText.includes(monthTarget) && hdrText.includes(yearTarget)) {
                        searchRoot = cal;
                        break;
                    }
                }

                // Fallback: tüm menu
                if (!searchRoot) searchRoot = menu;

                // Çoklu selector stratejisi
                const SELECTORS = [
                    '.dp__cell_inner',
                    '.dp__calendar_item button',
                    '.dp__calendar_item > div',
                    '.dp__calendar_item',
                ];

                for (const sel of SELECTORS) {
                    const candidates = Array.from(searchRoot.querySelectorAll(sel))
                        .filter(c => {
                            const text = c.textContent.trim();
                            const num  = parseInt(text, 10);
                            if (!text || isNaN(num)) return false;
                            const item = c.closest('.dp__calendar_item') ?? c;
                            return !item.classList.contains('dp__cell_offset') &&
                                   !c.classList.contains('dp__cell_offset');
                        });

                    const cell = candidates.find(c => parseInt(c.textContent.trim(), 10) === dayTarget);
                    if (cell) {
                        cell.scrollIntoView({ block: 'nearest' });
                        cell.click();
                        return true;
                    }
                }

                return false;
            })();
            """);
    }

    /// <summary>
    /// Açık takvimdeki ay/yıl başlığını okuyup hedef aya ulaşana dek ok tuşlarına basar.
    /// </summary>
    private async Task NavigateToMonthAsync(DateTime target)
    {
        var page = GetPage();

        for (var attempt = 0; attempt < 24; attempt++)
        {
            var currentText = await page.EvaluateExpressionAsync<string>($$"""
                (() => {
                    // Açık .dp__menu içindeki ay/yıl butonlarını oku.
                    // Tüm DOM'dan okumak birden fazla takvim varsa yanlış ay gösterir.
                    const menu = Array.from(document.querySelectorAll('.dp__menu'))
                        .find(m => {
                            const s = window.getComputedStyle(m);
                            const r = m.getBoundingClientRect();
                            return s.display !== 'none' && s.visibility !== 'hidden' && r.width > 0;
                        });
                    if (!menu) return '';
                    const headers = Array.from(menu.querySelectorAll('.dp__month_year_select'));
                    return headers.map(h => h.textContent.trim()).join(' ');
                })();
                """);

            if (IsTargetMonthVisible(currentText, target))
                break;

            // Hedef geçmişte mi, gelecekte mi?
            if (ShouldGoBack(currentText, target))
                await ClickCalendarNavAsync(forward: false);
            else
                await ClickCalendarNavAsync(forward: true);

            await WaitAsync(300);
        }
    }

    private async Task ClickCalendarNavAsync(bool forward)
    {
        var page = GetPage();
        var selector = forward
            ? ".dp__arrow_top, [data-dp-element='action-next'], button.dp__btn:last-of-type"
            : ".dp__arrow_top, [data-dp-element='action-prev'], button.dp__btn:first-of-type";

        // Yön düğmelerini metinle de bulmaya çalış
        var clicked = await page.EvaluateExpressionAsync<bool>($$"""
            (() => {
                // Önce data-dp-element
                const next = document.querySelector("[data-dp-element='action-next']");
                const prev = document.querySelector("[data-dp-element='action-prev']");
                const btn  = {{(forward ? "next" : "prev")}};
                if (btn) { btn.click(); return true; }

                // Yoksa SVG içeren ileri/geri butonları dene
                const navBtns = Array.from(document.querySelectorAll('.dp__nav_btn'));
                const target  = {{(forward ? "navBtns[navBtns.length - 1]" : "navBtns[0]")}};
                if (target) { target.click(); return true; }

                return false;
            })();
            """);

        if (!clicked)
            await ShowDebugAsync("Takvim navigasyon butonu bulunamadı.");
    }

    // ─── Saat Seçimi ──────────────────────────────────────────────────────────

    /// <summary>
    /// Yolcu360'ın özel saat dropdown'ını açıp istenilen saati seçer.
    /// timePickerIndex: 0 = Alış Saati, 1 = Bırakış Saati
    /// </summary>
    private async Task SelectTimeAsync(int timePickerIndex, string time)
    {
        var page = GetPage();

        if (string.IsNullOrWhiteSpace(time))
            return;

        var timeJson  = JsonSerializer.Serialize(time.Trim());
        var indexJson = JsonSerializer.Serialize(timePickerIndex);

        // Saat kutusu: her tarih grubundaki ikinci büyük div (alış=0, bırakış=1)
        var opened = await page.EvaluateExpressionAsync<bool>($$"""
            (() => {
                // Her iki büyük tarih+saat grubu
                const groups = document.querySelectorAll(
                    '[modaltitle="Alış ve Bırakış Tarihi"]');
                const group  = groups[{{indexJson}}];
                if (!group) return false;

                // Grup içindeki ikinci büyük div = saat kutusu
                const timeBox = group.querySelectorAll(':scope > div')[1];
                if (!timeBox) return false;

                timeBox.click();
                return true;
            })();
            """);

        if (!opened)
        {
            await ShowDebugAsync($"Saat picker[{timePickerIndex}] açılamadı, atlanıyor.");
            return;
        }

        await WaitAsync(500);

        // Dropdown içinden zaman seçeneğini bul ve tıkla
        var selected = await page.EvaluateExpressionAsync<bool>($$"""
            (() => {
                const target = {{timeJson}};

                // Açık dropdown option'larını tara
                const options = Array.from(document.querySelectorAll(
                    '.dropdown-item, [role="option"], li, .time-option'));

                const found = options.find(o =>
                    o.textContent.trim() === target ||
                    o.textContent.trim().startsWith(target));

                if (found) { found.click(); return true; }

                // Fallback: tüm görünür buton ve li'lerde ara
                const all = Array.from(document.querySelectorAll('div, li, span, button'))
                    .filter(el => {
                        const t = el.textContent.trim();
                        return (t === target || t.startsWith(target)) && el.children.length === 0;
                    });

                if (all.length > 0) { all[0].click(); return true; }
                return false;
            })();
            """);

        if (!selected)
            await ShowDebugAsync($"Saat '{time}' dropdown'da bulunamadı, varsayılan bırakıldı.");
        else
            await ShowDebugAsync($"Saat seçildi: {time}");

        await WaitAsync(300);
    }

    // ─── Arama Butonu ─────────────────────────────────────────────────────────

    private async Task ClickSearchButtonAsync()
    {
        var page = GetPage();

        await page.WaitForSelectorAsync(Selectors.SearchButton, new WaitForSelectorOptions
        {
            Visible = true,
            Timeout = 30_000
        });

        // Açık dropdown/takvim varsa kapat; üstte kalıp buton tıklamasını yutmasın.
        await page.Keyboard.PressAsync("Escape");
        await WaitAsync(250);

        var clickPoint = await page.EvaluateExpressionAsync<ClickPoint>("""
            (() => {
                const btn = document.querySelector('#search');
                if (!btn) {
                    return { found: false, enabled: false, x: 0, y: 0, text: '' };
                }

                btn.scrollIntoView({ block: 'center', inline: 'center' });

                const rect = btn.getBoundingClientRect();
                const style = window.getComputedStyle(btn);
                const enabled =
                    !btn.disabled &&
                    btn.getAttribute('aria-disabled') !== 'true' &&
                    style.pointerEvents !== 'none' &&
                    rect.width > 0 &&
                    rect.height > 0;

                return {
                    found: true,
                    enabled,
                    x: rect.left + rect.width / 2,
                    y: rect.top + rect.height / 2,
                    text: btn.textContent.trim()
                };
            })();
            """);

        if (!clickPoint.Found)
            throw new InvalidOperationException("Araç Ara butonu DOM'da bulunamadı.");

        if (!clickPoint.Enabled)
            throw new InvalidOperationException($"Araç Ara butonu pasif görünüyor. Buton yazısı: {clickPoint.Text}");

        var beforeUrl = page.Url;

        // Önce gerçek fare tıklaması yapılıyor.
        await page.Mouse.ClickAsync(clickPoint.X, clickPoint.Y);
        await WaitAsync(750);

        var changedAfterMouseClick = await HasSearchStartedAsync(beforeUrl);

        if (!changedAfterMouseClick)
        {
            // Framework listener'ları için pointer/mouse/click olaylarını sırayla gönder.
            await page.EvaluateExpressionAsync("""
                (() => {
                    const btn = document.querySelector('#search');
                    if (!btn) return;

                    btn.scrollIntoView({ block: 'center', inline: 'center' });
                    btn.focus();

                    btn.dispatchEvent(new PointerEvent('pointerdown', {
                        bubbles: true,
                        cancelable: true,
                        pointerType: 'mouse',
                        isPrimary: true
                    }));
                    btn.dispatchEvent(new MouseEvent('mousedown', {
                        bubbles: true,
                        cancelable: true,
                        view: window
                    }));
                    btn.dispatchEvent(new PointerEvent('pointerup', {
                        bubbles: true,
                        cancelable: true,
                        pointerType: 'mouse',
                        isPrimary: true
                    }));
                    btn.dispatchEvent(new MouseEvent('mouseup', {
                        bubbles: true,
                        cancelable: true,
                        view: window
                    }));
                    btn.dispatchEvent(new MouseEvent('click', {
                        bubbles: true,
                        cancelable: true,
                        view: window
                    }));
                    btn.click();
                })();
                """);

            await WaitAsync(750);
        }

        if (!await HasSearchStartedAsync(beforeUrl))
        {
            var diag = await GetDiagnosticAsync();
            throw new InvalidOperationException($"Araç Ara butonu tıklandı ama sayfa aramayı başlatmadı. {diag}");
        }

        Report("Araç Ara butonu tıklandı.");
        await ShowDebugAsync("Araç Ara butonu tıklandı.");
    }

    private async Task<bool> HasSearchStartedAsync(string beforeUrl)
    {
        var page = GetPage();

        return await page.EvaluateExpressionAsync<bool>($$"""
            (() => {
                const beforeUrl = {{JsonSerializer.Serialize(beforeUrl)}};
                const text = document.body.innerText.toLocaleLowerCase('tr-TR');

                return window.location.href !== beforeUrl
                    || window.location.href.includes('search')
                    || window.location.href.includes('arac-kiralama')
                    || window.location.href.includes('list')
                    || text.includes('sonuç')
                    || text.includes('araç bulundu')
                    || text.includes('kirala')
                    || text.includes('lütfen bir alış yeri seçin');
            })();
            """);
    }

    // ─── Sonuç Bekleme ────────────────────────────────────────────────────────

    private async Task WaitForSearchResultAsync()
    {
        var page = GetPage();

        try
        {
            await page.WaitForFunctionAsync(
                """
                () => {
                    const text = document.body.innerText.toLocaleLowerCase('tr-TR');
                    return window.location.href.includes('search')
                        || window.location.href.includes('arac-kiralama')
                        || window.location.href.includes('list')
                        || text.includes('tl')
                        || text.includes('araç bulundu')
                        || text.includes('kirala')
                        || text.includes('lütfen bir alış yeri seçin');
                }
                """,
                new WaitForFunctionOptions { Timeout = 20_000 });
        }
        catch (WaitTaskTimeoutException)
        {
            var diag = await GetDiagnosticAsync();
            throw new InvalidOperationException($"Arama tetiklendi ama sonuç gelmedi. {diag}");
        }

        var bodyText = await GetBodyTextAsync();

        if (bodyText.Contains("Lütfen bir alış yeri seçin", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "Yolcu360, alış yeri seçimini geçersiz saydı. " +
                "Autocomplete listesinden kayıtlı bir konum seçilmesi gerekiyor.");
    }

    // ─── Filtreler ────────────────────────────────────────────────────────────

    private async Task ClickOptionalFilterAsync(string? selector)
    {
        if (string.IsNullOrWhiteSpace(selector))
            return;

        var page = GetPage();

        try
        {
            await page.WaitForSelectorAsync(selector, new WaitForSelectorOptions
            {
                Visible = true,
                Timeout = 8_000
            });

            await page.EvaluateExpressionAsync($$"""
                (() => {
                    const el = document.querySelector({{JsonSerializer.Serialize(selector)}});
                    el?.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true, view: window }));
                })();
                """);
        }
        catch (WaitTaskTimeoutException)
        {
            // Filtre DOM'da yoksa akış devam eder.
        }
    }

    private static string? GetTransmissionSelector(string transmissionType) =>
        transmissionType.Trim().ToLowerInvariant() switch
        {
            "automatic" or "otomatik" => Selectors.AutomaticTransmissionFilter,
            "manual"    or "manuel"   => Selectors.ManualTransmissionFilter,
            _                         => null
        };

    private static string? GetFuelSelector(string fuelType) =>
        fuelType.Trim().ToLowerInvariant() switch
        {
            "diesel"  or "dizel"  => Selectors.DieselFuelFilter,
            "gasoline" or "benzin" => Selectors.GasolineFuelFilter,
            _                      => null
        };

    // ─── Yardımcı Metotlar ────────────────────────────────────────────────────

    /// <summary>Nuxt SSR hydration'ın tamamlanması için küçük bir fare hareketi yapar.</summary>
    private async Task WarmUpHydrationAsync()
    {
        var page = GetPage();
        await page.Mouse.MoveAsync(20, 20);
        await page.Mouse.ClickAsync(20, 20);

        try
        {
            await page.WaitForFunctionAsync(
                "() => document.readyState === 'complete' && !!document.querySelector('#inputPickUpLocation')",
                new WaitForFunctionOptions { Timeout = 10_000 });
        }
        catch
        {
            // Hydration sinyali okunamazsa arama yine denenecek.
        }
    }

    private async Task CloseInitialPopupAsync()
    {
        var page = GetPage();

        try
        {
            var closed = await page.EvaluateExpressionAsync<bool>(
                """
                (() => {
                    const closeButton = document.querySelector('.gs_trigger_discount_popup_close_container');
                    if (!closeButton) return false;

                    const rect = closeButton.getBoundingClientRect();
                    const style = window.getComputedStyle(closeButton);
                    const visible = rect.width > 0 &&
                        rect.height > 0 &&
                        style.display !== 'none' &&
                        style.visibility !== 'hidden';

                    if (!visible) return false;

                    closeButton.click();
                    return true;
                })();
                """);

            if (closed)
                await WaitAsync(600);
        }
        catch
        {
            // Popup bulunamazsa akış devam eder.
        }
    }

    /// <summary>Vue native setter + olay zincirleme — React/Vue formları için.</summary>
    private async Task NativeSetInputAsync(string selector, string value)
    {
        var page      = GetPage();
        var selJson   = JsonSerializer.Serialize(selector);
        var valJson   = JsonSerializer.Serialize(value);

        await page.EvaluateExpressionAsync($$"""
            (() => {
                const el = document.querySelector({{selJson}});
                if (!el) throw new Error('Element not found: ' + {{selJson}});

                const proto = el instanceof HTMLTextAreaElement
                    ? HTMLTextAreaElement.prototype
                    : HTMLInputElement.prototype;

                Object.getOwnPropertyDescriptor(proto, 'value').set.call(el, {{valJson}});
                el.dispatchEvent(new Event('input',  { bubbles: true }));
                el.dispatchEvent(new Event('change', { bubbles: true }));
                el.dispatchEvent(new KeyboardEvent('keyup', { bubbles: true }));
            })();
            """);
    }

    private static string NormalizePhoneNumber(string phoneNumber)
    {
        var digits = new string(phoneNumber.Where(char.IsDigit).ToArray());

        if (digits.StartsWith("90", StringComparison.Ordinal) && digits.Length == 12)
            digits = digits[2..];

        if (digits.StartsWith("0", StringComparison.Ordinal) && digits.Length == 11)
            digits = digits[1..];

        return digits;
    }

    private void Report(string message) => ProgressChanged?.Invoke(message);

    private async Task ShowDebugAsync(string message)
    {
        var page    = GetPage();
        var msgJson = JsonSerializer.Serialize(message);

        await page.EvaluateExpressionAsync($$"""
            (() => {
                let panel = document.querySelector('#_y360_debug');
                if (!panel) {
                    panel = document.createElement('div');
                    panel.id = '_y360_debug';
                    Object.assign(panel.style, {
                        position: 'fixed', left: '12px', top: '12px',
                        zIndex: '2147483647', padding: '10px 14px',
                        background: '#111827', color: '#f9fafb',
                        font: '13px -apple-system, sans-serif', borderRadius: '8px',
                        boxShadow: '0 8px 24px rgba(0,0,0,.35)', maxWidth: '520px'
                    });
                    document.body.appendChild(panel);
                }
                panel.textContent = {{msgJson}};
            })();
            """);
    }

    private async Task<string> GetBodyTextAsync()
    {
        var page = GetPage();
        return await page.EvaluateExpressionAsync<string>(
            "document.body?.innerText || ''");
    }

    private async Task<string> GetDiagnosticAsync()
    {
        var page = GetPage();
        var url  = page.Url;
        var text = (await GetBodyTextAsync())
            .Replace('\n', ' ')
            .Replace('\r', ' ');

        if (text.Length > 240)
            text = text[..240];

        return $"URL: {url}. Sayfa: {text}";
    }

    public async Task SaveCurrentSessionAsync()
    {
        var page = GetPage();
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

        await File.WriteAllTextAsync(SessionStateFilePath, json);
        Report("Oturum kaydedildi.");
    }

    private async Task TryRestoreSessionAsync()
    {
        if (!File.Exists(SessionStateFilePath))
            return;

        SessionState? state;

        try
        {
            var json = await File.ReadAllTextAsync(SessionStateFilePath);
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

    private static Task WaitAsync(int ms) => Task.Delay(ms);

    // ─── Takvim Yardımcıları ──────────────────────────────────────────────────

    private static bool IsTargetMonthVisible(string headerText, DateTime target)
    {
        if (string.IsNullOrWhiteSpace(headerText))
            return false;

        var turkishMonths = new[]
        {
            "Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran",
            "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık"
        };

        var monthName = turkishMonths[target.Month - 1];
        var yearStr   = target.Year.ToString();

        return headerText.Contains(monthName, StringComparison.OrdinalIgnoreCase)
            && headerText.Contains(yearStr);
    }

    private static bool DoesDisplayedDateLookLikeTarget(string displayedText, DateTime target)
    {
        if (string.IsNullOrWhiteSpace(displayedText))
            return false;

        var turkishMonths = new[]
        {
            "Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran",
            "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık"
        };

        var compact = displayedText
            .Replace('\n', ' ')
            .Replace('\r', ' ')
            .Trim();

        var day = target.Day.ToString();
        var dayWithZero = target.Day.ToString("00");
        var month = target.Month.ToString();
        var monthWithZero = target.Month.ToString("00");
        var year = target.Year.ToString();
        var monthName = turkishMonths[target.Month - 1];

        return compact.Contains(target.ToString("yyyy-MM-dd"), StringComparison.OrdinalIgnoreCase)
            || compact.Contains(target.ToString("dd.MM.yyyy"), StringComparison.OrdinalIgnoreCase)
            || compact.Contains(target.ToString("d.M.yyyy"), StringComparison.OrdinalIgnoreCase)
            || (
                compact.Contains(year, StringComparison.OrdinalIgnoreCase)
                && compact.Contains(monthName, StringComparison.OrdinalIgnoreCase)
                && (
                    compact.Contains($" {day} ", StringComparison.OrdinalIgnoreCase)
                    || compact.Contains($" {day}.", StringComparison.OrdinalIgnoreCase)
                    || compact.Contains($"{day} {monthName}", StringComparison.OrdinalIgnoreCase)
                )
            )
            || (
                compact.Contains(year, StringComparison.OrdinalIgnoreCase)
                && (compact.Contains($".{monthWithZero}.", StringComparison.OrdinalIgnoreCase)
                    || compact.Contains($".{month}.", StringComparison.OrdinalIgnoreCase))
                && (compact.Contains(dayWithZero, StringComparison.OrdinalIgnoreCase)
                    || compact.Contains(day, StringComparison.OrdinalIgnoreCase))
            );
    }

    private static bool ShouldGoBack(string headerText, DateTime target)
    {
        if (string.IsNullOrWhiteSpace(headerText))
            return false;

        // Yıl sayısı eşleşmiyorsa karşılaştır
        foreach (var part in headerText.Split(' '))
        {
            if (int.TryParse(part, out var year))
            {
                if (year > target.Year) return true;
                if (year < target.Year) return false;
                break;
            }
        }

        // Aynı yıl — ay indeksine göre karar ver
        var turkishMonths = new[]
        {
            "Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran",
            "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık"
        };

        for (var i = 0; i < turkishMonths.Length; i++)
        {
            if (headerText.Contains(turkishMonths[i], StringComparison.OrdinalIgnoreCase))
                return (i + 1) > target.Month;
        }

        return false;
    }

    // ─── IAsyncDisposable ─────────────────────────────────────────────────────

    private IPage GetPage()
    {
        return _page ?? throw new InvalidOperationException(
            "InitializeAsync çağrılmadan tarayıcı kullanılamaz.");
    }

    private sealed class ClickPoint
    {
        [JsonPropertyName("found")]
        public bool Found { get; init; }

        [JsonPropertyName("enabled")]
        public bool Enabled { get; init; }

        [JsonPropertyName("x")]
        public decimal X { get; init; }

        [JsonPropertyName("y")]
        public decimal Y { get; init; }

        [JsonPropertyName("text")]
        public string Text { get; init; } = "";

        [JsonPropertyName("index")]
        public int Index { get; init; }
    }

    private sealed class SessionState
    {
        public DateTimeOffset SavedAt { get; init; }
        public string CurrentUrl { get; init; } = "";
        public CookieParam[] Cookies { get; init; } = [];
        public Dictionary<string, string?> LocalStorage { get; init; } = [];
        public Dictionary<string, string?> SessionStorage { get; init; } = [];
    }

    public async ValueTask DisposeAsync()
    {
        if (_page is not null)
            await _page.CloseAsync();

        if (_browser is not null)
            await _browser.CloseAsync();
    }
}

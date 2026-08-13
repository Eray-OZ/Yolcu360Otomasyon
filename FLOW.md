# Register -> Search -> Collection -> Payment Akisi

Bu dokuman, uygulamada bir kullanicinin kayit olmasindan baslayip arama yapmasi, arama sonucunu koleksiyon olarak kaydetmesi ve kaydedilen koleksiyondaki bir arac icin iyzico sandbox odemesi yapmasina kadar calisan kodlari sirasiyla aciklar.

Ama burada anlatilan akisin merkezi nokta su: UI olaylari `MainWindow` parcalarinda baslar, is kurallari servis siniflarina dagilir, tarayici islemleri `BAService` ile yapilir, kalici veri `DatabaseService` ile MySQL tarafina yazilir.

## 0. Uygulama Acilirken Hazirlanan Alanlar

Dosya: `MainWindow.axaml.cs`

```csharp
private readonly DatabaseService _databaseService = new(AppSettings.GetConnectionString());
private readonly DynamicCollectionService _dynamicCollectionService;
private readonly LocationSuggestionService _locationSuggestionService = new();
private readonly SmsReceiverService _smsReceiverService = new(5001);
private readonly IyzicoCallbackService _iyzicoCallbackService = new();
private readonly IyzicoPaymentService _iyzicoPaymentService;
```

Bu alanlar uygulama boyunca kullanilan servisleri tutar.

`_databaseService`, `key.json` veya ayar dosyasindan gelen connection string ile MySQL baglantisini hazirlar. Register, login, koleksiyon kaydi, arac kaydi ve odeme kaydi bu servis uzerinden yapilir.

`_dynamicCollectionService`, koleksiyon kaydetme ve koleksiyon verilerini sonradan guncelleme islerini `DatabaseService` etrafinda daha okunabilir bir katmana tasir.

`_locationSuggestionService`, uygulama icindeki alis yeri textbox'ina yazarken Yolcu360 uyumlu lokasyon onerileri almak icin kullanilir. Bu akisin ana register-search-payment kisminda zorunlu degildir ama arama inputuna yardimci olur.

`_smsReceiverService`, MacroDroid'den gelen SMS mesajlarini yakalamak icin lokal HTTP dinleyicisidir. Port sabit olarak `5001` verilmis. MacroDroid URL'i de bu porta istek atmalidir.

`_iyzicoCallbackService`, iyzico sandbox odemesi bittikten sonra iyzico'nun uygulamaya geri dondugu callback endpoint'ini acar.

`_iyzicoPaymentService`, iyzico SDK ile checkout oturumu olusturur, callback bekler ve odeme sonucunu iyzico'dan geri sorgular.

```csharp
private AppUser? _activeUser;
private List<SearchResultItem> _latestResults = new();
private List<SearchResultItem> _selectedCollectionVehicles = new();
private SearchResultItem? _selectedVehicle;
private KoleksiyonListItem? _selectedCollection;
private List<KoleksiyonListItem> _selectedCollections = new();
private List<OdemeHazirlikItem> _paymentPreviewItems = new();
private SearchFilter? _latestSearchFilter;
```

Bu alanlar ekranlar arasinda tasinan gecici durumdur.

`_activeUser`, uygulamada giris yapmis kullaniciyi tutar. Register veya login basarili olunca dolar. Arama, koleksiyon kaydi ve odeme islemleri bu kullanici olmadan calismaz.

`_latestResults`, son aramada Yolcu360 sayfasindan okunan arac listesidir. DataGrid'e bu liste basilir ve koleksiyon kaydinda bu liste veritabanina yazilir.

`_latestSearchFilter`, son aramada kullanilan alis yeri, tarih, saat, vites ve yakit bilgilerini tutar. Koleksiyon kaydedilirken sadece araclar degil, aramanin hangi kriterlerle yapildigi de kaydedilir.

`_selectedCollection`, gecmis kayitlar ekraninda secili olan tek koleksiyon bilgisidir.

`_selectedCollections`, DataGrid coklu secim yaptiginda birden fazla koleksiyonu tutar.

`_selectedCollectionVehicles`, secilen koleksiyonun icindeki araclari tutar.

`_selectedVehicle`, koleksiyon icinden odeme yapilacak secili aractir.

`_paymentPreviewItems`, iyzico odemesi baslamadan once hangi koleksiyon/arac icin ne kadar tutar gonderilecegini tutar.

```csharp
public MainWindow()
{
    InitializeComponent();
    _dynamicCollectionService = new DynamicCollectionService(_databaseService);
    _iyzicoPaymentService = new IyzicoPaymentService(AppSettings.GetIyzicoSettings(), _iyzicoCallbackService);
    PickupDateTextBox.Text = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    ReturnDateTextBox.Text = DateTime.Today.AddDays(2).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    PickupTimeTextBox.Text = "10:00";
    ReturnTimeTextBox.Text = "18:00";
    ConfigureResultsGrid();
    ConfigureCollectionsGrid();
    ConfigurePaymentsGrid();
    _smsReceiverService.SmsReceived += SmsReceiverService_SmsReceived;
    _ = _databaseService.EnsureDatabaseAsync();
    InitializeSmsReceiver();
    _activeUser = null;
    ShowLoginView();
}
```

`InitializeComponent()`, XAML'deki butonlari, textboxlari, panelleri ve DataGrid'leri C# tarafinda kullanilabilir hale getirir.

`DynamicCollectionService`, constructor icinde uretilir cunku `DatabaseService` nesnesine ihtiyac duyar.

`IyzicoPaymentService`, ayarlardan gelen iyzico keyleri ve callback servisi ile uretilir.

Tarih ve saat textboxlari baslangicta varsayilan deger alir. Alis tarihi bugun, donus tarihi iki gun sonra, saatler ise `10:00` ve `18:00` olur.

`ConfigureResultsGrid`, `ConfigureCollectionsGrid`, `ConfigurePaymentsGrid` DataGrid kolonlarini kod tarafinda ayarlar. Yani arama sonucu, koleksiyon ve odeme listeleri hangi kolonlarla gorunecek burada hazirlanir.

`_smsReceiverService.SmsReceived += SmsReceiverService_SmsReceived`, SMS alindiginda ekrandaki status yazisini guncellemek icin event baglar.

`EnsureDatabaseAsync`, uygulama acilirken veritabani tablolarinin hazir olmasini baslatir.

`InitializeSmsReceiver`, MacroDroid'den gelecek SMS HTTP isteklerini dinlemeye baslar.

`ShowLoginView`, uygulamayi ilk acilista login ekraninda baslatir.

## 1. Register Akisi

### 1.1 Register Butonuna Basilir

Dosya: `MainWindow/MainWindow.Auth.cs`

```csharp
private async void RegisterButton_Click(object? sender, RoutedEventArgs e)
```

Bu metod, kullanici kayit ekraninda register butonuna basinca calisir.

```csharp
RegisterButton.IsEnabled = false;
RegisterStatusTextBlock.Text = "Kullanıcı kaydı hazırlanıyor...";
```

Buton gecici olarak kapatilir. Bunun sebebi kullanicinin ayni anda iki kere kayit istegi baslatmasini engellemektir.

Status yazisi, kayit isleminin basladigini gosterir.

```csharp
var email = RegisterEmailTextBox.Text?.Trim() ?? string.Empty;
var password = RegisterPasswordTextBox.Text?.Trim() ?? string.Empty;
var phoneNumber = RegisterPhoneNumberTextBox.Text?.Trim() ?? string.Empty;
```

UI'daki email, sifre ve telefon textboxlarindan degerler okunur.

`?.Trim()` bastaki ve sondaki bosluklari temizler.

`?? string.Empty`, textbox null donerse bos string kullanir.

```csharp
if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(phoneNumber))
{
    RegisterStatusTextBlock.Text = "Email, şifre ve telefon numarası zorunlu.";
    return;
}
```

Email, sifre veya telefon bos ise kayit devam etmez. Burada veritabanina gitmeden once basit UI dogrulamasi yapilir.

```csharp
if (await _databaseService.UserExistsAsync(email))
{
    RegisterStatusTextBlock.Text = "Bu email zaten kayıtlı.";
    return;
}
```

Ayni email ile daha once kayit var mi diye veritabanina bakilir. Varsa yeni kullanici olusturulmaz.

### 1.2 Session Dosya Yolu Hazirlanir

```csharp
var sessionStatePath = BuildSessionStatePath(email);
if (File.Exists(sessionStatePath))
{
    try { File.Delete(sessionStatePath); } catch { }
}
```

`BuildSessionStatePath(email)`, bu kullanicinin Yolcu360 oturum bilgilerinin saklanacagi JSON dosyasinin yolunu uretir.

Kayit islemi yeni kullanici gibi davranacagi icin ayni email'e ait eski session dosyasi varsa silinir. Bu sayede eski cookie/localStorage bilgileri yeni kayit akisini etkilemez.

`try/catch` bos birakilmis; dosya silinemezse kayit tamamen patlamasin diye hata yutulur.

```csharp
private static string BuildSessionStatePath(string email)
{
    var safeFileName = string.Concat(email.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_'));
    var sessionsDirectory = Path.Combine(ResolveAppDataDirectory(), "sessions");
    return Path.Combine(sessionsDirectory, $"{safeFileName}.json");
}
```

Email dosya adina cevrilirken harf/rakam disindaki karakterler `_` yapilir. Ornegin `test@mail.com`, `test_mail_com.json` gibi bir dosyaya donusur.

`ResolveAppDataDirectory()` proje klasorunu bulur. Sonra proje icindeki `sessions` klasorunun altina kullaniciya ait JSON dosya yolu uretilir.

```csharp
private static string ResolveAppDataDirectory()
{
    var current = new DirectoryInfo(AppContext.BaseDirectory);

    while (current is not null)
    {
        if (File.Exists(Path.Combine(current.FullName, "Yolcu360Otomasyon.csproj")))
            return current.FullName;

        current = current.Parent;
    }

    return AppContext.BaseDirectory;
}
```

Uygulama calisirken `AppContext.BaseDirectory` genelde `bin/Debug/net8.0` gibi derleme klasorudur. Bu metod ust klasorlere cika cika `Yolcu360Otomasyon.csproj` dosyasini arar. Buldugu klasoru proje kok dizini kabul eder. Bulamazsa calisma klasorunu kullanir.

### 1.3 Kullanici Veritabanina Kaydedilir

```csharp
await _databaseService.SaveOrUpdateUserAsync(email, password, phoneNumber, sessionStatePath);
```

Bu satir kullaniciyi `kullanicilar` tablosuna kaydeder. Ayni metod hem yeni kullanici ekleyebilir hem var olan kullaniciyi guncelleyebilir.

Dosya: `Services/Database/DatabaseService.Users.cs`

```csharp
public async Task SaveOrUpdateUserAsync(string email, string password, string phoneNumber, string sessionStatePath)
{
    await EnsureSchemaAsync();
    await using var context = new AppDbContext(_options);
    var existingUser = await context.Kullanicilar.FirstOrDefaultAsync(user => user.Email == email);
    var now = DateTime.UtcNow;
```

`EnsureSchemaAsync`, veritabani semasinin hazir oldugundan emin olur.

`AppDbContext`, EF Core uzerinden veritabanina baglanmak icin acilir.

`existingUser`, ayni email ile kullanici var mi diye arar.

`now`, kayit veya guncelleme zamanini UTC olarak tutar.

```csharp
if (existingUser is null)
{
    context.Kullanicilar.Add(new AppUser
    {
        Email = email,
        Password = password,
        PhoneNumber = phoneNumber,
        SessionStatePath = sessionStatePath,
        CreatedAt = now,
        UpdatedAt = now
    });
}
```

Kullanici yoksa yeni `AppUser` nesnesi olusturulur.

`Email`, uygulamaya giris icin kullanilir.

`Password`, mevcut kodda duz metin tutuluyor. Sunumda bunu belirtmek gerekir; gercek uygulamada hash kullanilmalidir.

`PhoneNumber`, Yolcu360'a telefon ile giris yapmak icin gereklidir.

`SessionStatePath`, Yolcu360 cookie/localStorage/sessionStorage bilgilerinin kaydedilecegi dosya yoludur.

`CreatedAt` ve `UpdatedAt`, kullanicinin olusturma ve guncelleme zamanlaridir.

```csharp
else
{
    existingUser.Password = password;
    existingUser.PhoneNumber = phoneNumber;
    existingUser.SessionStatePath = sessionStatePath;
    existingUser.UpdatedAt = now;
}
```

Kullanici varsa yeni kayit acilmaz, mevcut kullanici guncellenir.

```csharp
await context.SaveChangesAsync();
```

EF Core tarafinda yapilan ekleme/guncelleme veritabanina yazilir.

### 1.4 Kayit Sonrasi Otomatik Login Baslar

```csharp
LoginEmailTextBox.Text = email;
LoginPasswordTextBox.Text = password;
StatusTextBlock.Text = "Kayıt oluşturuldu. Gömülü tarayıcıda giriş başlatılıyor...";

await PerformLoginAsync(email, password, forceBrowserLogin: true);
```

Kayit basarili olunca login ekranindaki email/sifre alanlari doldurulur.

Sonra `PerformLoginAsync` cagrilir.

`forceBrowserLogin: true` onemlidir. Bu deger, kayit sonrasi session dosyasi olsa bile tarayici ile telefon/SMS login akisinin zorlanmasini saglar.

### 1.5 PerformLoginAsync Kullanici Bilgisini Kontrol Eder

Dosya: `MainWindow/MainWindow.Auth.cs`

```csharp
private async Task PerformLoginAsync(string email, string password, bool forceBrowserLogin = false)
```

Bu metod hem normal login butonundan hem de register sonrasi otomatik login icin kullanilir.

```csharp
LoginButton.IsEnabled = false;
SetNavigationEnabled(false);
```

Login butonu kapatilir. Navigasyon da kapatilir. Kullanici login devam ederken arama/gecmis/odeme sekmelerine gecemesin diye `_isAuthenticating` aktif hale gelir.

```csharp
StatusTextBlock.Text = "Kullanıcı bilgileri kontrol ediliyor...";
var user = await _databaseService.GetUserByCredentialsAsync(email, password);
if (user is null)
{
    StatusTextBlock.Text = "Kullanıcı bulunamadı veya şifre hatalı.";
    return;
}
```

Email ve sifre ile veritabaninda kullanici aranir.

Dosya: `DatabaseService.Users.cs`

```csharp
return await context.Kullanicilar
    .AsNoTracking()
    .FirstOrDefaultAsync(user => user.Email == email && user.Password == password);
```

`AsNoTracking`, bu sorgunun sadece okuma amacli oldugunu soyler. EF Core nesneyi takip etmez, bu da gereksiz tracking maliyetini azaltir.

Email ve sifre eslesirse `AppUser` doner, eslesmezse `null` doner.

### 1.6 Session Varsa Tarayici Acilmadan Ana Sayfaya Gidilir

```csharp
var sessionStatePath = BuildSessionStatePath(email);
if (!forceBrowserLogin && File.Exists(sessionStatePath))
{
    _activeUser = new AppUser
    {
        Id = user.Id,
        Email = email,
        Password = password,
        PhoneNumber = user.PhoneNumber,
        SessionStatePath = sessionStatePath
    };

    StatusTextBlock.Text = "Kayıtlı oturum bulundu.";
    ShowMainView();
    await LoadHistoryAsync();
    return;
}
```

Normal login yapiliyorsa ve session dosyasi varsa tarayici login akisi calismaz.

`_activeUser` doldurulur. Bundan sonra uygulama kullaniciyi giris yapmis kabul eder.

`ShowMainView()` ana ekrani acar.

`LoadHistoryAsync()` bu kullanicinin koleksiyonlarini veritabanindan okur.

`return`, SMS ve Yolcu360 login akisini tamamen atlar.

Kayit sonrasi `forceBrowserLogin: true` verildigi icin bu blok atlanir.

### 1.7 Browser Login Gorunur Hale Getirilir

```csharp
LoginView.IsVisible = false;
RegisterView.IsVisible = false;
MainView.IsVisible = true;
ShowBrowserSection();
SetNavigationVisibility(false);
```

Login/register ekranlari kapanir.

Ana view acilir.

`ShowBrowserSection`, tarayici panelini gosterir. Boylece kullanici Yolcu360 login akisini uygulama icinde gorebilir.

Navigasyon gizlenir. Login bitmeden sekmelerle karisik islem yapilmasi engellenir.

### 1.8 BAService Olusturulur

```csharp
var baService = CreateBAService();
await baService.ClearBrowserSessionAsync();
```

Dosya: `MainWindow/MainWindow.Search.cs`

```csharp
private BAService CreateBAService()
{
    var baService = new BAService(EmbeddedBrowser);
    baService.ProgressChanged += message =>
    {
        Dispatcher.UIThread.Post(() =>
        {
            SearchStatusTextBlock.Text = message;
        });
    };

    return baService;
}
```

`BAService`, XAML'deki `EmbeddedBrowser` kontrolunu alir.

Bu servis yeni Chrome acmaz; NativeWebView uzerinde JavaScript calistirir.

`ProgressChanged` eventi, otomasyon servisinden gelen ilerleme mesajlarini UI thread uzerinden `SearchStatusTextBlock` alanina yazar.

`ClearBrowserSessionAsync`, onceki Yolcu360 cookie/localStorage/sessionStorage verilerini temizler. Register sonrasi temiz login istenildigi icin cagirilir.

### 1.9 Telefon ile Yolcu360 Login

```csharp
_smsReceiverService.ClearLatestCode();
await baService.LoginWithPhoneAsync(user.PhoneNumber);
```

`ClearLatestCode`, onceki denemeden kalmis SMS kodunu temizler.

`LoginWithPhoneAsync`, Yolcu360 login ekranini acar, telefon numarasini yazar ve SMS ekranina gelene kadar bekler.

Dosya: `Services/BrowserAutomation/BAService.Auth.cs`

```csharp
public async Task LoginWithPhoneAsync(string phoneNumber)
{
    if (string.IsNullOrWhiteSpace(phoneNumber))
        throw new InvalidOperationException("Telefon numarası boş bırakılamaz.");
```

Telefon numarasi bos ise otomasyon baslamaz.

```csharp
await NavigateAsync("https://www.yolcu360.com/login?redirect=%2F");
await WaitForDocumentReadyAsync();
await EnsureJavaScriptHelpersAsync();
await InjectStealthAndHumanMouseScriptAsync();
await WaitForInitialPopupAndCloseAsync(TimeSpan.FromSeconds(5));
```

`NavigateAsync`, NativeWebView'i login URL'ine goturur ve navigation tamamlanana kadar bekler.

`WaitForDocumentReadyAsync`, `document.readyState` degeri `complete` olana kadar bekler.

`EnsureJavaScriptHelpersAsync`, tarayiciya `window.__ba` helper nesnesini ekler. Bu helper icinde metin normalize etme, gorunurluk kontrolu ve insansi click fonksiyonu vardir.

`InjectStealthAndHumanMouseScriptAsync`, `navigator.webdriver`, `navigator.plugins`, `navigator.languages` gibi otomasyon izlerini azaltmaya calisan JS parcasi ekler. Ayni zamanda recaptcha hatasini yakalamak icin `window.__hasRecaptchaScoreError` fonksiyonu eklenir.

`WaitForInitialPopupAndCloseAsync`, popup gelirse kapatir.

```csharp
await WaitForScriptTrueAsync(
    """
    (() => !!document.querySelector('#phn-input') || !!document.querySelector('input[type="tel"]'))();
    """,
    TimeSpan.FromSeconds(20));
```

Telefon inputu DOM'a gelene kadar beklenir.

`#phn-input` asil hedef selector'dur. Yedek olarak `input[type="tel"]` kontrol edilir.

```csharp
var normalizedPhone = NormalizePhoneNumber(phoneNumber);
```

`NormalizePhoneNumber`, numaradaki rakam disi karakterleri siler. Basinda `90` veya `0` varsa Turkiye lokal 10 haneli formata cevirir.

```csharp
await WaitForPhoneInputReadyAsync();
```

Bu metod inputun gorunur, aktif ve readonly olmayan hale gelmesini bekler.

```csharp
input.focus();
input.click();
input.value = '';
input.dispatchEvent(new Event('input', { bubbles: true }));
```

Input odaklanir, tiklanir, eski degeri temizlenir ve Vue/Nuxt state'inin degisimi algilamasi icin `input` eventi gonderilir.

```csharp
var phoneChunks = SplitPhoneNumber(normalizedPhone);
foreach (var chunk in phoneChunks)
{
    foreach (var ch in chunk)
    {
        ...
        await Task.Delay(Random.Shared.Next(110, 170));
    }
    await Task.Delay(Random.Shared.Next(180, 320));
}
```

Telefon numarasi tek seferde value atamasi olarak yazilmaz. Parcalara bolunur ve karakter karakter yazilir.

Her karakter icin `keydown`, `input`, `keyup` eventleri uretilir. Bu, sitedeki input maskesi ve Vue binding mekanizmasinin degeri algilamasi icindir.

Buradaki kisa `Task.Delay` gecikmeleri captcha davranis sinyali icin kasitli bir istisnadir. Diger beklemelerde sabit sure beklemek yerine DOM kosulu beklenmeye calisiliyor.

```csharp
input.dispatchEvent(new Event('change', { bubbles: true }));
input.dispatchEvent(new Event('blur', { bubbles: true }));
```

Numara yazildiktan sonra `change` ve `blur` eventi gonderilir. Bazi formlar validasyonu input yazilirken degil, inputtan cikinca calistirir.

```csharp
const btn = Array.from(document.querySelectorAll('button, input[type="submit"]'))
    .find(b => (b.textContent || b.value || '').trim().toLowerCase().includes('devam'));
if (btn) {
    btn.disabled = false;
    btn.removeAttribute('disabled');
    btn.classList.remove('disabled');
}
```

Devam butonu text ile bulunur. Buton disabled kaldiysa acilir. Bu kisim, form validasyonu gecikirse butonun tiklanabilir olmasini saglamak icin var.

```csharp
for (int i = 0; i < 4; i++)
{
    var rx = Random.Shared.Next(100, 500);
    var ry = Random.Shared.Next(100, 400);
    await EvaluateScriptAsync($"window.__dispatchHumanMousePath ? window.__dispatchHumanMousePath({rx}, {ry}) : null;");
    await Task.Delay(500);
}
```

Telefon yazildiktan sonra random mouse hareketi eventleri gonderilir. Bu kisim recaptcha v3 davranis puani icin eklenmis davranis simule etme bolumudur.

```csharp
const btn = Array.from(document.querySelectorAll('button, input[type="submit"], [role="button"]'))
    .find(b => {
        const txt = (b.textContent || b.value || b.getAttribute('aria-label') || '').trim().toLowerCase();
        return txt.includes('devam');
    });
...
btn.click();
```

Devam butonu bulunur, gorunur alana getirilir, disabled attribute'lari temizlenir ve tiklanir.

```csharp
var hasRecaptchaError = await WaitForSmsScreenOrRecaptchaErrorAsync(TimeSpan.FromSeconds(8));
```

Bu metod iki olasılıktan birini bekler:

1. SMS kod inputu gorundu.
2. Sayfada recaptcha score hatasi gorundu.

Recaptcha hatasi algilanirsa kisa bir tekrar denemesi yapilir.

```csharp
await WaitForScriptTrueAsync(
    """
    (() => {
        const input = document.querySelector('#sms_input');
        return !!window.__ba?.isVisible(input);
    })();
    """,
    TimeSpan.FromSeconds(30));
```

Login akisi SMS dogrulama ekranina gelmeden tamamlanmis sayilmaz. Burada asil beklenen input `#sms_input` selectorudur.

### 1.10 SMS Kodu Uygulamaya Gelir

```csharp
code = await _smsReceiverService.WaitForCodeAsync(TimeSpan.FromMinutes(2));
```

`SmsReceiverService`, MacroDroid'den gelen HTTP isteginden SMS mesajini okur ve icindeki dogrulama kodunu yakalar.

`WaitForCodeAsync`, 2 dakika icinde kod gelmezse hata firlatir. Hata mesajinda MacroDroid URL formatini da gosterir.

### 1.11 SMS Kodu Tarayiciya Yazilir

```csharp
await baService.FillSmsVerificationCodeAsync(code);
```

Dosya: `BAService.Auth.cs`

```csharp
if (string.IsNullOrWhiteSpace(code))
    throw new InvalidOperationException("SMS doğrulama kodu boş olamaz.");
```

Bos kod kabul edilmez.

```csharp
await WaitForSmsCodeInputReadyAsync();
```

`#sms_input` inputunun gorunur, aktif ve readonly olmayan hale gelmesi beklenir.

```csharp
const input = document.querySelector('#sms_input');
```

SMS kodunun yazilacagi kesin input `#sms_input` ile bulunur.

```csharp
const descriptor = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value');
if (descriptor?.set) {
    descriptor.set.call(input, code);
} else {
    input.value = code;
}
```

Input degeri dogrudan set edilir. Descriptor kullanilmasinin sebebi, framework kontrollu inputlarda native setter'i kullanarak Vue/React benzeri binding'in degisimi daha dogru algilamasidir.

```csharp
input.dispatchEvent(new InputEvent('input', {
    bubbles: true,
    inputType: 'insertText',
    data: code
}));

input.dispatchEvent(new Event('change', { bubbles: true }));
```

Kod yazildiktan sonra `input` ve `change` eventleri gonderilir. Site bu eventlerle form state'ini gunceller.

```csharp
await WaitForSmsVerificationButtonReadyAsync(TimeSpan.FromSeconds(8));
```

`button[data-cms-key="button_apply"]` selectorune sahip Dogrula butonunun hazir olmasi beklenir.

```csharp
const button = document.querySelector('button[data-cms-key="button_apply"]');
button.disabled = false;
button.removeAttribute('disabled');
button.classList.remove('disabled');
button.click();
```

Dogrula butonu bulunur, disabled durumlari temizlenir ve tiklanir.

### 1.12 Login Tamamlanir ve Session Kaydedilir

```csharp
await baService.WaitForLoginCompletedAsync();
```

Bu metod localStorage icindeki `user` ve `token` alanlarini kontrol eder.

```javascript
const user = JSON.parse(localStorage.getItem('user') || 'null');
const token = JSON.parse(localStorage.getItem('token') || 'null');

return !!user &&
    user.anonymous === false &&
    !!token &&
    typeof token.accessToken === 'string' &&
    token.accessToken.length > 0;
```

`user.anonymous === false`, artik anonim ziyaretci degil gercek kullanici oldugunu gosterir.

`token.accessToken`, Yolcu360 API'leri icin gerekli access token'in localStorage'a yazildigini gosterir.

```csharp
await baService.SaveSessionAsync(sessionStatePath);
```

Login tamamlaninca cookie, localStorage ve sessionStorage JSON dosyasina kaydedilir.

```csharp
var cookiesRaw = await EvaluateScriptAsync("document.cookie");
```

Sayfadaki cookie string'i okunur.

```csharp
for (let i = 0; i < localStorage.length; i++) {
    const key = localStorage.key(i);
    if (key) result[key] = localStorage.getItem(key);
}
```

Tarayicidaki tum localStorage anahtar/degerleri JS ile okunur.

```csharp
for (let i = 0; i < sessionStorage.length; i++) {
    const key = sessionStorage.key(i);
    if (key) result[key] = sessionStorage.getItem(key);
}
```

Ayni islem sessionStorage icin de yapilir.

```csharp
var state = new EmbeddedSessionState
{
    SavedAt = DateTimeOffset.UtcNow,
    CurrentUrl = currentUrl,
    Cookies = cookies,
    LocalStorage = localStorage,
    SessionStorage = sessionStorage
};
```

Tum oturum bilgileri tek modelde toplanir.

```csharp
await File.WriteAllTextAsync(filePath, json);
```

Session JSON dosyaya yazilir.

```csharp
await _databaseService.SaveOrUpdateUserAsync(email, password, user.PhoneNumber, sessionStatePath);
```

Kullanici tablosundaki session path tekrar guncellenir.

```csharp
_activeUser = new AppUser
{
    Id = user.Id,
    Email = email,
    Password = password,
    PhoneNumber = user.PhoneNumber,
    SessionStatePath = sessionStatePath
};
```

Aktif kullanici set edilir. Bundan sonra arama, koleksiyon ve odeme islemleri bu kullanici uzerinden ilerler.

## 2. Search Akisi

### 2.1 Search Butonuna Basilir

Dosya: `MainWindow/MainWindow.Search.cs`

```csharp
private async void SearchButton_Click(object? sender, RoutedEventArgs e)
```

Arama ekranindaki butona basilinca bu metod calisir.

```csharp
SearchButton.IsEnabled = false;
SearchStatusTextBlock.Text = "Arama hazırlanıyor...";
```

Arama baslarken buton kapatilir. Ayni anda ikinci arama baslatilmasi engellenir.

```csharp
DateTime.TryParseExact(PickupDateTextBox.Text?.Trim(), "yyyy-MM-dd", ...)
DateTime.TryParseExact(ReturnDateTextBox.Text?.Trim(), "yyyy-MM-dd", ...)
```

Alis ve donus tarihleri textboxlardan okunur ve `yyyy-MM-dd` formatinda parse edilir. Format bozuksa arama baslamaz.

```csharp
var pickupTime = PickupTimeTextBox.Text?.Trim() ?? "10:00";
var returnTime = ReturnTimeTextBox.Text?.Trim() ?? "18:00";
```

Saat alanlari okunur. Bos gelirse varsayilan saatler kullanilir.

```csharp
var filter = new SearchFilter
{
    PickupLocation = PickupLocationTextBox.Text?.Trim() ?? string.Empty,
    PickupDate = pickupDate.Date,
    ReturnDate = returnDate.Date,
    PickupTime = pickupTime,
    ReturnTime = returnTime,
    TransmissionType = GetComboBoxTag(TransmissionComboBox),
    FuelType = GetComboBoxTag(FuelComboBox)
};
_latestSearchFilter = filter;
```

Kullanicinin arama kriterleri `SearchFilter` modelinde toplanir.

`_latestSearchFilter`, koleksiyon kaydinda kullanilmak uzere saklanir.

```csharp
private static string GetComboBoxTag(ComboBox comboBox)
{
    return (comboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? string.Empty;
}
```

Vites ve yakit combobox'larinda gorunen metin yerine `Tag` degeri okunur. Bu, filtre kodunun `otomatik`, `manuel`, `dizel`, `benzin` gibi temiz degerlerle calismasini saglar.

```csharp
if (string.IsNullOrWhiteSpace(filter.PickupLocation))
```

Alis yeri bos ise arama baslamaz.

```csharp
if (_activeUser is null)
```

Kullanici login olmadan arama yapamaz. Cunku arama sonrasi koleksiyon ve session kullaniciya baglidir.

### 2.2 Session Restore Edilir

```csharp
var baService = CreateBAService();
if (_activeUser is not null && !string.IsNullOrWhiteSpace(_activeUser.SessionStatePath))
{
    await baService.RestoreSessionAsync(_activeUser.SessionStatePath);
}
```

Tarayici otomasyonu icin `BAService` olusturulur.

Eger kullanicinin session dosyasi varsa, Yolcu360 localStorage/cookie bilgileri tarayiciya geri yuklenir.

Dosya: `BAService.Auth.cs`

```csharp
var json = await File.ReadAllTextAsync(filePath);
var state = JsonSerializer.Deserialize<EmbeddedSessionState>(json);
```

Session JSON dosyasi okunur ve `EmbeddedSessionState` modeline cevrilir.

```csharp
foreach (var part in cookieParts)
{
    var partJson = JsonSerializer.Serialize(part.Trim() + "; path=/; domain=.yolcu360.com");
    await EvaluateScriptAsync($"document.cookie = {partJson};");
}
```

Kaydedilen cookie'ler tekrar `document.cookie` uzerinden tarayiciya yazilir.

```csharp
localStorage.setItem(key, items[key]);
sessionStorage.setItem(key, items[key]);
```

Kaydedilen localStorage ve sessionStorage degerleri tekrar siteye enjekte edilir.

Bu islem sayesinde kullanici arama yaparken Yolcu360 tarafinda tekrar telefon login yapmak zorunda kalmaz.

### 2.3 Yolcu360 Ana Sayfasi Acilir

```csharp
await baService.OpenYolcu360HomeAsync();
```

Dosya: `BAService.cs`

```csharp
await NavigateAsync(Yolcu360HomeUrl);
```

NativeWebView, `https://www.yolcu360.com/` adresine gider.

```csharp
await WaitForDocumentReadyAsync();
```

Sayfanin temel yuklenmesi tamamlanana kadar beklenir.

```csharp
await EnsureJavaScriptHelpersAsync();
```

Tarayici icine ortak JS helperlari eklenir:

`normalizeText`, metindeki fazla bosluklari temizler.

`normalizeTr`, metni Turkce locale ile kucuk harfe cevirir.

`compactTr`, normalize edilmis metindeki bosluklari tamamen kaldirir.

`isVisible`, elementin DOM'da gercekten gorunur olup olmadigini kontrol eder.

`clickLikeUser`, elementi ortaya getirir, mouse/pointer eventleri ve click eventini gonderir.

```csharp
var popupClosed = await WaitForInitialPopupAndCloseAsync(TimeSpan.FromSeconds(5));
```

Site acilis popup'i varsa kapatilir. Popup yoksa akisa devam edilir.

### 2.4 Alis Yeri Yazilir ve Autocomplete Secilir

```csharp
await baService.FillPickupLocationAsync(filter.PickupLocation);
```

Dosya: `BAService.SearchForm.cs`

```csharp
if (string.IsNullOrWhiteSpace(location))
    throw new InvalidOperationException("Alış yeri boş bırakılamaz.");
```

Bos lokasyon kabul edilmez.

```csharp
var locationJson = JsonSerializer.Serialize(location.Trim());
var pickupLocationInputSelectorJson = JsonSerializer.Serialize(PickupLocationInputSelector);
var locationSuggestionSelectorJson = JsonSerializer.Serialize(LocationSuggestionSelector);
```

C# stringleri JavaScript icine guvenli gomulebilmek icin JSON string'e cevrilir.

`PickupLocationInputSelector`, `#inputPickUpLocation` selectorudur.

`LocationSuggestionSelector`, desktop/mobile autocomplete itemlerini kapsayan selector listesidir.

```csharp
await WaitForScriptTrueAsync(
    $$"""
    (() => !!document.querySelector({{pickupLocationInputSelectorJson}}))();
    """,
    TimeSpan.FromSeconds(20));
```

Alis yeri inputu DOM'a gelene kadar beklenir.

```javascript
const input = document.querySelector("#inputPickUpLocation");
input.focus();
input.value = '';
input.dispatchEvent(new InputEvent('input', ...));
```

Input bulunur, odaklanir, eski deger temizlenir ve siteye inputun degistigi bildirilir.

```javascript
for (const char of text) {
    input.dispatchEvent(new KeyboardEvent('keydown', { bubbles: true, key: char }));
    input.value += char;
    input.dispatchEvent(new InputEvent('input', { bubbles: true, inputType: 'insertText', data: char }));
    input.dispatchEvent(new KeyboardEvent('keyup', { bubbles: true, key: char }));
}
```

Lokasyon karakter karakter yazilir. Her karakter icin klavye ve input eventleri gonderilir. Bu, Yolcu360 autocomplete sisteminin gercek yazim gibi calismasi icindir.

```csharp
await WaitForLocationSuggestionsAsync(LocationSuggestionSelector, TimeSpan.FromSeconds(12));
```

Autocomplete listesindeki gorunur oneriler beklenir. Bu metod sure beklemez; DOM'da gorunur suggestion sayisi 0'dan buyuk olana kadar kontrol eder.

```javascript
const getScore = item => {
    const fullText = normalize(item.textContent || '');
    const mainText = getMainText(item);
    const compactText = compact(item.textContent || '');

    if (mainText === target) return 0;
    if (compactText === compact(`${targetText} Türkiye`) || compactText === compact(`${targetText}, Türkiye`)) return 1;
    if (fullText === target) return 2;
    if (mainText.startsWith(target)) return 3;
    if (fullText.startsWith(target)) return 4;
    if (mainText.includes(target)) return 5;
    if (fullText.includes(target)) return 6;
    return 7;
};
```

Autocomplete secimi puanlama ile yapilir.

`mainText`, onerinin ana basligini ifade eder. Ornegin sadece `İstanbul`.

`fullText`, onerinin tum yazisidir. Ornegin `İstanbul Türkiye`.

`compactText`, bosluklari silinmis metindir. `İstanbulTürkiye` gibi birlesik gelen HTML'lerde ise yarar.

En dusuk skor en iyi eslesmedir. Tam ana baslik eslesmesi en onceliklidir. Boylece `İstanbul` yazildiginda ustte havaalani ciksa bile ana metni tam `İstanbul` olan secenek daha iyi skor alir.

```javascript
const items = Array.from(document.querySelectorAll(locationSuggestionSelector))
    .filter(item => isVisible(item) && (!input || (item !== input && !item.contains(input))));
```

Tum oneriler alinir, gorunmeyenler elenir. Inputun kendisi veya inputu kapsayan elementler secenek sanilmasin diye listeden cikarilir.

```javascript
const selected = items.sort(...)[0];
```

Skora gore siralama yapilir. Skor esit ise ekrandaki konuma gore ustteki secilir.

```javascript
const clickResult = window.__ba.clickLikeUser(selected, locationSuggestionSelector);
```

Secilen onerinin uzerine insansi click uygulanir.

```csharp
selectionApplied = await WaitForPickupLocationSelectionAppliedAsync(TimeSpan.FromSeconds(3));
```

Click yapildiktan sonra secimin gercekten uygulanip uygulanmadigi kontrol edilir.

```javascript
const hasPickupText = !!pickupInput && pickupInput.value.trim().length > 0;
return hasPickupText && openSuggestions.length === 0;
```

Alis yeri inputunda metin varsa ve acik autocomplete onerisi kalmamissa secim basarili sayilir.

Bu islem 3 deneme yapar. 3 denemede de secim uygulanmazsa hata firlatir.

### 2.5 Tarih Araligi Secilir

```csharp
await baService.SelectDateRangeAsync(filter.PickupDate, filter.ReturnDate);
```

Bu metod alis ve donus tarihlerini tek datepicker uzerinden secer.

```csharp
var opened = await OpenDatePickerAsync();
```

Tarih secici acilir.

`OpenDatePickerAsync`, once ekranda `Alış Tarihi` veya `Alış ve Bırakış Tarihi` textini arar. Bu textin en yakin datepicker kapsayicisini bulur. Bulamazsa sabit selectorlara duser:

`DatePickerSelector = ".dp__main.dp__theme_light"`

`DateTimeGroupSelector = "[modaltitle='Alış ve Bırakış Tarihi']"`

Bulunan elemente pointer/mouse/click eventleri gonderilir. Icinde input veya ikon varsa ona da ayni tetikleme yapilir.

```csharp
await WaitForDatePickerMenuAsync(TimeSpan.FromSeconds(10));
```

`.dp__menu`, `.dp__outer_menu_wrap` veya `.dp__calendar` gorunur olana kadar bekler.

```csharp
await NavigateToMonthAsync(pickupDate);
```

Takvim basliginda hedef ay/yil gorunmuyorsa ileri/geri oklarla hedef aya gidilir.

`NavigateToMonthAsync`, gorunur takvim menulerinden ay/yil basligini okur. `IsTargetMonthVisible` ile hedef ay gorunuyor mu bakar. Degilse `ShouldGoBack` ile ileri mi geri mi gidilecegine karar verir ve `ClickCalendarNavAsync` ile yon butonuna tiklar.

```csharp
var pickupSelected = await ClickCalendarDayAsync(pickupDate);
```

Alis gunu tiklanir.

`ClickCalendarDayAsync`, hedef gun, ay adi ve yil bilgisini JavaScript'e verir. JS once gorunur datepicker menusunu bulur. Sonra hedef ay/yil basligina ait calendar root'unu bulur. Bu ayrim onemlidir; cunku datepicker ayni anda iki ay gosterebilir.

Calendar root bulunduktan sonra `.dp__cell_inner`, `.dp__calendar_item button`, `.dp__calendar_item > div`, `.dp__calendar_item` selectorleri icinden gun hucreleri aranir.

Offset veya disabled hucreler secilmez. Boylece onceki/sonraki aya ait ayni gun numarasina yanlis tiklanmasi engellenir.

Hedef gun bulunursa scroll edilir, pointer/mouse eventleri ve `click()` gonderilir.

```csharp
await WaitForCalendarSelectionStateAsync(pickupDate, TimeSpan.FromSeconds(2));
```

Secilen gunun class veya `aria-selected` durumunda secili isareti var mi diye kontrol eder. Bu sadece click atildi mi degil, site secimi kabul etti mi kontroludur.

```csharp
if (returnDate.Year != pickupDate.Year || returnDate.Month != pickupDate.Month)
{
    await NavigateToMonthAsync(returnDate);
}
```

Donus tarihi farkli ayda ise datepicker tekrar hedef aya gezdirilir.

```csharp
var returnSelected = await ClickCalendarDayAsync(returnDate);
```

Donus gunu de ayni gun tiklama algoritmasi ile secilir.

```csharp
await ConfirmDatePickerAsync();
await WaitForDatePickerClosedAsync(TimeSpan.FromSeconds(4));
```

Datepicker icindeki sec/onay butonu tiklanir. Sonra takvim menusu kapanana kadar beklenir.

### 2.6 Saatler Secilir

```csharp
await baService.SelectTimeAsync(0, filter.PickupTime);
await baService.SelectTimeAsync(1, filter.ReturnTime);
```

`timePickerIndex = 0`, alis saatidir.

`timePickerIndex = 1`, donus saatidir.

```javascript
const groups = document.querySelectorAll('[modaltitle="Alış ve Bırakış Tarihi"], [modaltitlecmskey="pickup_and_dropoff_date"]');
```

Sayfadaki tarih/saat gruplari bulunur.

```javascript
const group = groups[index];
const timeBox = group.querySelectorAll(':scope > div')[1] || group.querySelector('select, input, div[class*="time"]');
```

Ilgili grubun saat kutusu bulunur. Once grubun ikinci direkt child div'i denenir, olmazsa select/input/time class fallback'i kullanilir.

```javascript
timeBox.click();
```

Saat dropdown'u acilir.

```csharp
await WaitForTimeOptionVisibleAsync(time.Trim(), TimeSpan.FromSeconds(5));
```

Dropdown icinde hedef saat texti gorunur olana kadar beklenir.

```javascript
const options = Array.from(document.querySelectorAll('.dropdown-item, [role="option"], li, .time-option, div[class*="option"], div[class*="item"]'))
    .filter(visible);
```

Saat secenekleri olabilecek elementler toplanir.

```javascript
let found = options.find(o => {
    const txt = (o.textContent || '').trim();
    return txt === target || txt.startsWith(target);
});
```

Hedef saat ile tam eslesen veya onunla baslayan secenek bulunur.

```javascript
found.scrollIntoView(...);
found.dispatchEvent(...);
found.click();
```

Secenek gorunur alana getirilir, mouse/pointer eventleri ve click gonderilir.

```csharp
await WaitForTimeSelectionAppliedAsync(timePickerIndex, time.Trim(), TimeSpan.FromSeconds(3));
```

Secimden sonra ilgili tarih/saat grubunun textinde hedef saat var mi veya input/select degeri hedef saat mi diye kontrol edilir.

### 2.7 Arama Butonuna Basilir

```csharp
await baService.ClickSearchButtonAsync();
```

```javascript
const active = document.activeElement;
if (active && typeof active.blur === 'function') {
    active.blur();
}
```

Aktif inputtan cikilir. Bu, acik autocomplete veya datepicker state'inin kapanmasina yardim eder.

```javascript
document
    .querySelectorAll('.dp__menu, .search-autocomplete')
    .forEach(menu => {
        menu.style.display = 'none';
    });
```

Ekranda arama butonunun ustunu kapatabilecek acik menu varsa gizlenir.

```csharp
await WaitForFloatingMenusClosedAsync(TimeSpan.FromSeconds(3));
```

Autocomplete veya datepicker menuleri gorunmez olana kadar beklenir.

```javascript
const btn = document.querySelector('#search');
```

Arama butonu direkt `#search` id'si ile bulunur.

```javascript
const isClickable =
    rect.width > 0 &&
    rect.height > 0 &&
    style.display !== 'none' &&
    style.visibility !== 'hidden' &&
    style.pointerEvents !== 'none' &&
    !btn.disabled &&
    btn.getAttribute('aria-disabled') !== 'true';
```

Buton gorunur, tiklanabilir ve disabled degil mi kontrol edilir.

```javascript
btn.dispatchEvent(new MouseEvent('mousedown', ...));
btn.dispatchEvent(new MouseEvent('mouseup', ...));
btn.click();
```

Arama butonuna mouse down, mouse up ve click gonderilir.

### 2.8 Sonuclar Beklenir

```csharp
await baService.WaitForSearchResultsAsync();
```

Dosya: `BAService.Results.cs`

```javascript
const cards = Array.from(document.querySelectorAll('#car_card_list .car-card'));
return cards.some(window.__ba?.isVisible || (() => false));
```

Sonuc kartlari `#car_card_list .car-card` selectoru ile aranir.

En az bir gorunur kart varsa sonuc sayfasi hazir kabul edilir.

Bu bekleme, aramadan hemen sonra sayfanin sonuc kartlarini yuklemesi zaman aldigi icin vardir.

### 2.9 Vites ve Yakit Filtreleri Uygulanir

```csharp
await baService.ApplyResultFiltersAsync(filter);
```

Filtreler arama butonundan sonra sonuc sayfasinda uygulanir.

```csharp
var hasTransmission = !string.IsNullOrWhiteSpace(filter.TransmissionType);
var hasFuel = !string.IsNullOrWhiteSpace(filter.FuelType);

if (!hasTransmission && !hasFuel) return;
```

Vites veya yakit secilmemisse filtreleme yapilmaz.

```csharp
await WaitForResultFiltersReadyAsync(TimeSpan.FromSeconds(8));
```

Sonuc sayfasindaki filtre paneli hazir olana kadar beklenir.

```javascript
const filterContainer = document.querySelector('.filter-container');
```

Filtrelerin ana kapsayicisi bulunur.

```javascript
const filterControl = filterContainer.querySelector(
    'label[name^="filter-transmission."], ' +
    'label[name^="filter-fuel."], ' +
    'input[id^="filter-transmission."], ' +
    'input[id^="filter-fuel."]'
);
```

Vites/yakit filtre label veya inputlari aranir.

```csharp
var targetTexts = transmissionNorm switch
{
    "otomatik" or "automatic" => new[] { "otomatik" },
    "manuel" or "manual" => new[] { "manuel" },
    _ => Array.Empty<string>()
};
```

Uygulamadaki vites degeri Yolcu360 filtre textine cevrilir.

```csharp
await ClickFilterOptionAsync("Vites filtresi", "filter-transmission", targetTexts);
```

`filter-transmission` ile baslayan label'lar arasindan hedef text bulunur.

```javascript
const labels = Array.from(document.querySelectorAll(`label[name^="${prefix}."]`))
    .filter(isVisible);
```

Gorunur filtre label'lari toplanir.

```javascript
const match = labels.find(label => matchesTarget(normalize(label.textContent || '')));
```

Label texti normalize edilerek hedef filtre degeriyle eslestirilir.

```javascript
match.click();
const input = match.querySelector('input[type="checkbox"], input[type="radio"]');
if (input && !input.checked) {
    input.click();
    input.dispatchEvent(new Event('change', { bubbles: true }));
}
```

Once label tiklanir. Label icinde checkbox/radio varsa ve halen checked degilse input da tiklanir, sonra `change` eventi gonderilir.

Yakit filtresi de ayni mantikla `filter-fuel` prefix'i uzerinden uygulanir.

Filtrelerden sonra tekrar `WaitForSearchResultsAsync` cagrilir. Cunku filtre uygulandiginda sonuc listesi yenilenir.

### 2.10 Sonuc Kartlari Okunur

```csharp
var results = await baService.ReadSearchResultsAsync();
_latestResults = results;
```

Sonuc sayfasindaki kartlar okunur ve `_latestResults` alanina yazilir.

```javascript
const cards = Array.from(document.querySelectorAll('#car_card_list .car-card'))
    .filter(isVisible);
```

Gorunur arac kartlari alinir.

```javascript
const specs = Array.from(card.querySelectorAll('.icon-gear-type, .icon-gas-type'))
    .map(icon => normalize(icon.parentElement?.textContent))
    .filter(Boolean);
```

Vites ve yakit bilgileri ikonlarin parent textlerinden okunur.

```javascript
const title = firstVisibleText(card, '.text-dark-gray.text-lg.font-bold');
const subtitle = firstVisibleText(card, '[data-cms-key="or_similar"]');
const price = firstVisibleText(card, '#car_total_price');
const dailyPrice = firstVisibleText(card, '[data-cms-key="text_daily_price2"]');
```

Arac basligi, benzer arac metni, toplam fiyat ve gunluk fiyat kart icindeki sabit selectorlardan okunur.

```javascript
const transmission = specs.find(text => /manuel|otomatik/i.test(text)) || '';
const fuelType = specs.find(text => /benzin|dizel|hibrit|hybrid|elektrik|electric/i.test(text)) || '';
```

Specs listesinden vites ve yakit textleri regex ile ayrilir.

```javascript
const supplier = normalize(card.querySelector('figure img[alt]')?.getAttribute('alt'));
const pickupInfo = normalize(card.querySelector('.icon-filled')?.parentElement?.textContent);
const actionText = firstVisibleText(card, '[data-cms-key="button_rent_now"]');
const url = normalize(card.querySelector('a[href]')?.getAttribute('href'));
```

Firma adi image alt attribute'undan, teslim bilgisi icon parent textinden, kirala butonu texti ve link de kart icindeki ilgili elementlerden okunur.

```javascript
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
```

Her kart `SearchResultItem` modeline uyacak JSON objesine cevrilir.

```csharp
var items = await EvaluateJsonScriptAsync<List<SearchResultItem>>(...)
```

JS tarafindan `JSON.stringify(items)` olarak donen liste C# tarafinda `List<SearchResultItem>` modeline deserialize edilir.

### 2.11 Sonuclar DataGrid'e Yazilir

```csharp
await Dispatcher.UIThread.InvokeAsync(() =>
{
    ResultsDataGrid.ItemsSource = null;
    ResultsDataGrid.ItemsSource = _latestResults;
    SearchResultsPanel.IsVisible = _latestResults.Count > 0;
});
```

DataGrid UI thread uzerinden guncellenir.

Once `ItemsSource = null` yapilir. Sonra yeni liste atanir. Bu, DataGrid'in listeyi yeniden render etmesini saglar.

Sonuc varsa sonuc paneli gorunur hale gelir.

## 3. Arama Sonucunu Koleksiyon Olarak Kaydetme

### 3.1 Kaydet Butonuna Basilir

Dosya: `MainWindow/MainWindow.Search.cs`

```csharp
private async void SaveResultsButton_Click(object? sender, RoutedEventArgs e)
```

Arama sonuclari ekrandayken kullanici koleksiyon adi girip kaydet butonuna basinca calisir.

```csharp
if (_activeUser is null)
```

Kullanici login degilse kayit yapilmaz.

```csharp
if (_activeUser.Id <= 0)
{
    var latestUser = await _databaseService.GetUserByEmailAsync(_activeUser.Email);
    ...
    _activeUser = latestUser;
}
```

Aktif kullanicinin ID'si gecersizse veritabanindan email ile tekrar kullanici cekilir. Bu, yeni kayit sonrasi UI state'inde ID eksik kalirsa koleksiyon kaydinin patlamasini engeller.

```csharp
if (_latestResults.Count == 0)
```

Son aramada sonuc yoksa koleksiyon kaydi yapilmaz.

```csharp
if (_latestSearchFilter is null)
```

Aramanin kriterleri yoksa kayit yapilmaz. Cunku koleksiyon sadece arac listesinden ibaret degildir; hangi kriterlerle bulundugu da saklanir.

```csharp
var ozelAd = CollectionNameTextBox.Text?.Trim() ?? string.Empty;
```

Kullanicinin girdigi koleksiyon adi okunur.

```csharp
var collectionId = await _dynamicCollectionService.SaveSnapshotAsync(_activeUser.Id, ozelAd, _latestSearchFilter, _latestResults);
```

Koleksiyon kaydi baslar.

Dosya: `Services/Collections/DynamicCollectionService.cs`

```csharp
public Task<int> SaveSnapshotAsync(
    int kullaniciId,
    string ozelAd,
    SearchFilter filter,
    IReadOnlyCollection<SearchResultItem> currentResults)
{
    return _databaseService.SaveCollectionAsync(kullaniciId, ozelAd, filter, currentResults);
}
```

Bu metod su an direkt `DatabaseService.SaveCollectionAsync` metodunu cagirir. Ayrica katman olmasinin sebebi, dinamik koleksiyon guncelleme gibi ek islerin sonradan buradan yonetilebilmesidir.

### 3.2 Koleksiyon ve Araclar Veritabanina Yazilir

Dosya: `Services/Database/DatabaseService.Collections.cs`

```csharp
public async Task<int> SaveCollectionAsync(int kullaniciId, string ozelAd, SearchFilter filter, IReadOnlyCollection<SearchResultItem> items)
```

Bu metod bir koleksiyon row'u ve ona bagli arac row'larini kaydeder.

```csharp
await EnsureSchemaAsync();
await using var context = new AppDbContext(_options);
```

Veritabani hazirlanir ve EF Core context acilir.

```csharp
var kullaniciVarMi = await context.Kullanicilar
    .AsNoTracking()
    .AnyAsync(item => item.Id == kullaniciId);

if (!kullaniciVarMi)
    throw new InvalidOperationException("Aktif kullanıcı kaydı veritabanında bulunamadı.");
```

Koleksiyonun baglanacagi kullanici gercekten var mi kontrol edilir. Yoksa yetim koleksiyon olusmasin diye hata verilir.

```csharp
var koleksiyon = new Koleksiyon
{
    KullaniciId = kullaniciId,
    OzelAd = Truncate(ozelAd, 250),
    AlisYeri = Truncate(filter.PickupLocation, 250),
    AlisTarihi = filter.PickupDate,
    DonusTarihi = filter.ReturnDate,
    AlisSaati = Truncate(filter.PickupTime, 16),
    DonusSaati = Truncate(filter.ReturnTime, 16),
    SecilenVitesFiltresi = Truncate(filter.TransmissionType, 64),
    SecilenYakitFiltresi = Truncate(filter.FuelType, 64),
    OlusturmaTarihi = DateTime.UtcNow,
    Araclar = items.Select(item => new Arac { ... }).ToList()
};
```

`Koleksiyon` nesnesi arama kriterleriyle olusturulur.

`KullaniciId`, koleksiyonu aktif kullaniciya baglar.

`OzelAd`, kullanicinin verdigi kayit adidir.

`AlisYeri`, `AlisTarihi`, `DonusTarihi`, `AlisSaati`, `DonusSaati`, arama formundan gelen degerlerdir.

`SecilenVitesFiltresi` ve `SecilenYakitFiltresi`, sonuc sayfasinda uygulanan filtreleri saklar.

`OlusturmaTarihi`, koleksiyonun kaydedildigi zamani tutar.

`Araclar`, `_latestResults` listesindeki her `SearchResultItem` icin yeni bir `Arac` entity'si uretir.

```csharp
Araclar = items.Select(item => new Arac
{
    Baslik = Truncate(item.Title, 250),
    AltBaslik = Truncate(item.Subtitle, 250),
    Fiyat = Truncate(item.Price, 64),
    GunlukFiyat = Truncate(item.DailyPrice, 64),
    Vites = Truncate(item.Transmission, 64),
    Yakit = Truncate(item.FuelType, 64),
    Sirket = Truncate(item.Supplier, 128),
    TeslimBilgisi = Truncate(item.PickupInfo, 255),
    IslemMetni = Truncate(item.ActionText, 128),
    Baglanti = Truncate(item.Url, 1024)
}).ToList()
```

Her sonuc karti veritabanindaki `araclar` tablosuna uygun hale getirilir.

`Baslik`, arac adi.

`AltBaslik`, "veya benzeri" gibi alt bilgi.

`Fiyat`, toplam fiyat.

`GunlukFiyat`, gunluk fiyat.

`Vites`, manuel/otomatik.

`Yakit`, benzin/dizel vb.

`Sirket`, kiralama firmasi.

`TeslimBilgisi`, teslim noktasi bilgisi.

`IslemMetni`, karttaki aksiyon butonu metni.

`Baglanti`, karttaki link.

`Truncate`, veritabanindaki kolon uzunluklarina uygun olsun diye uzun stringleri keser.

```csharp
context.Koleksiyonlar.Add(koleksiyon);
await context.SaveChangesAsync();
return koleksiyon.Id;
```

Koleksiyon context'e eklenir. EF Core, navigation property olan `Araclar` listesini de koleksiyonla birlikte kaydeder.

Kayit sonrasi koleksiyon ID'si geri dondurulur.

### 3.3 Kayit Sonrasi Gecmis Ekrani Acilir

```csharp
CollectionNameTextBox.Text = string.Empty;
SearchStatusTextBlock.Text = $"{_latestResults.Count} sonuç \"{ozelAd}\" adıyla kaydedildi.";
await LoadHistoryAsync();
ShowHistorySection();
```

Koleksiyon adi textbox'i temizlenir.

Gecmis kayitlar veritabanindan yeniden yuklenir.

Gecmis sekmesi gosterilir.

```csharp
var collections = (CollectionsDataGrid.ItemsSource as IEnumerable<KoleksiyonListItem>)?.ToList() ?? new List<KoleksiyonListItem>();
var savedCollection = collections.FirstOrDefault(item => item.Id == collectionId);
if (savedCollection is not null)
    CollectionsDataGrid.SelectedItem = savedCollection;
```

Yeni kaydedilen koleksiyon DataGrid'de bulunur ve secili hale getirilir.

## 4. Koleksiyonu ve Araclarini Listeleme

### 4.1 Gecmis Sekmesi Acilir

Dosya: `MainWindow/MainWindow.History.cs`

```csharp
private async void HistoryTabButton_Click(object? sender, RoutedEventArgs e)
{
    if (_isAuthenticating) return;
    ShowHistorySection();
    await LoadHistoryAsync();
}
```

Kullanici auth akisindaysa gecmis sekmesine gecilmez.

Degilse gecmis paneli gosterilir ve koleksiyonlar yuklenir.

```csharp
private async Task LoadHistoryAsync()
{
    if (_activeUser is null)
        return;
```

Aktif kullanici yoksa gecmis yuklenmez.

```csharp
var collections = await _databaseService.GetCollectionsAsync(_activeUser.Id);
CollectionsDataGrid.ItemsSource = null;
CollectionsDataGrid.ItemsSource = collections;
```

Kullanicinin koleksiyonlari veritabanindan cekilir ve koleksiyon DataGrid'ine yazilir.

Dosya: `DatabaseService.Collections.cs`

```csharp
return await context.Koleksiyonlar
    .AsNoTracking()
    .Where(item => item.KullaniciId == kullaniciId)
    .OrderByDescending(item => item.OlusturmaTarihi)
    .Select(item => new KoleksiyonListItem { ... })
    .ToListAsync();
```

Sadece aktif kullanicinin koleksiyonlari alinir.

En yeni koleksiyon en ustte gorunsun diye olusturma tarihine gore azalan siralanir.

Entity yerine `KoleksiyonListItem` DTO'su donulur. DataGrid sadece ekranda lazim olan alanlari alir.

```csharp
AracSayisi = item.Araclar.Count
```

Her koleksiyonda kac arac oldugu hesaplanir.

### 4.2 Koleksiyon Secilir

```csharp
private async void CollectionsDataGrid_SelectionChanged(object? sender, SelectionChangedEventArgs e)
```

DataGrid'de secim degisince calisir.

```csharp
_selectedCollections = CollectionsDataGrid.SelectedItems?.OfType<KoleksiyonListItem>().ToList()
    ?? (CollectionsDataGrid.SelectedItem is KoleksiyonListItem single ? [single] : new List<KoleksiyonListItem>());
```

Birden fazla secim varsa hepsi listeye alinir. Tek secim varsa tek elemanli liste olusturulur. Hic secim yoksa bos liste olur.

```csharp
if (_selectedCollections.Count == 0)
{
    _selectedCollection = null;
    _selectedVehicle = null;
    _selectedCollectionVehicles = new List<SearchResultItem>();
    CollectionVehiclesDataGrid.ItemsSource = null;
    ClearSelectedCollectionSummary();
    return;
}
```

Secim yoksa aktif koleksiyon, aktif arac ve arac listesi temizlenir.

```csharp
_selectedCollection = _selectedCollections[0];
UpdateSelectedCollectionSummary(_selectedCollections);
```

Ilk secilen koleksiyon aktif koleksiyon kabul edilir. Sag taraftaki ozet alanlari guncellenir.

### 4.3 Koleksiyon Icindeki Araclar Acilir

```csharp
private async Task OpenSelectedCollectionVehiclesAsync()
```

Kullanici `Araçları Görüntüle` butonuna basinca veya koleksiyona cift tiklayinca calisir.

```csharp
_selectedCollectionVehicles = await _databaseService.GetCollectionVehiclesAsync(_selectedCollection.Id);
CollectionVehiclesDataGrid.ItemsSource = null;
CollectionVehiclesDataGrid.ItemsSource = _selectedCollectionVehicles;
```

Secili koleksiyonun araclari veritabanindan okunur ve arac DataGrid'ine yazilir.

Dosya: `DatabaseService.Collections.cs`

```csharp
return await context.Araclar
    .AsNoTracking()
    .Where(item => item.KoleksiyonId == koleksiyonId)
    .OrderBy(item => item.Id)
    .Select(item => new SearchResultItem { ... })
    .ToListAsync();
```

`araclar` tablosundan sadece secilen koleksiyona ait araclar alinir.

Veritabanindaki `Arac` entity'si tekrar UI'da kullanilan `SearchResultItem` modeline cevrilir. Boylece arama sonucu DataGrid'i ile koleksiyon arac DataGrid'i ayni modelle calisabilir.

```csharp
if (_selectedCollectionVehicles.Count > 0)
{
    CollectionVehiclesDataGrid.SelectedItem = _selectedCollectionVehicles[0];
    _selectedVehicle = _selectedCollectionVehicles[0];
}
```

Koleksiyonda arac varsa ilk arac otomatik secilir.

```csharp
VehiclesViewPanel.IsVisible = true;
```

Arac listesi paneli gosterilir.

### 4.4 Odeme Yapilacak Arac Secilir

```csharp
private void CollectionVehiclesDataGrid_SelectionChanged(object? sender, SelectionChangedEventArgs e)
{
    if (CollectionVehiclesDataGrid.SelectedItem is SearchResultItem vehicle)
    {
        _selectedVehicle = vehicle;
        VehicleStatusTextBlock.Text = $"{_selectedCollection?.OzelAd} - {vehicle.Title} seçildi ({vehicle.Price}).";
    }
}
```

Arac DataGrid'inde kullanici farkli bir arac secerse `_selectedVehicle` guncellenir.

Odeme olustururken tutar ve arac adi bu secili aractan alinacaktir.

## 5. Koleksiyondaki Bir Arac Icin Odeme

### 5.1 Odeme Olustur Butonuna Basilir

Dosya: `MainWindow/MainWindow.Payments.cs`

```csharp
private void CreatePaymentButton_Click(object? sender, RoutedEventArgs e)
```

Koleksiyon/arac ekranindan odeme baslatan metoddur.

```csharp
if (_activeUser is null || _selectedCollection is null)
{
    HistoryStatusTextBlock.Text = "Ödeme oluşturmak için lütfen bir koleksiyon seçin.";
    return;
}
```

Odeme icin aktif kullanici ve secili koleksiyon zorunludur.

```csharp
var vehicle = _selectedVehicle ?? _selectedCollectionVehicles.FirstOrDefault();
```

Eger kullanici bir arac sectiyse o kullanilir. Secmediyse koleksiyon arac listesindeki ilk arac kullanilir.

```csharp
if (vehicle is null)
{
    HistoryStatusTextBlock.Text = "Ödeme yapmak için lütfen koleksiyon içerisinden bir araç seçin.";
    return;
}
```

Koleksiyon icinde arac yoksa odeme olusturulmaz.

```csharp
var vehiclePrice = ParseVehiclePrice(vehicle.Price);
```

Aracin fiyat texti decimal tutara cevrilir.

```csharp
private static decimal ParseVehiclePrice(string? priceText)
{
    var parsed = DatabaseService.ParseCurrency(priceText ?? string.Empty);
    return parsed > 0 ? parsed : 100.00m;
}
```

`ParseCurrency`, `TL`, `TRY`, nokta, virgul gibi karakterleri temizleyerek sayisal tutar uretir. Parse edilemezse sandbox test akisinin devam etmesi icin `100.00` kullanilir.

```csharp
_paymentPreviewItems = new List<OdemeHazirlikItem>
{
    new OdemeHazirlikItem
    {
        KoleksiyonId = _selectedCollection.Id,
        KoleksiyonAdi = $"{_selectedCollection.OzelAd} ({vehicle.Title})",
        Tutar = vehiclePrice
    }
};
```

Odeme hazirlik listesi olusturulur.

`KoleksiyonId`, odemenin hangi koleksiyona baglanacagini belirtir.

`KoleksiyonAdi`, kullaniciya gosterilecek isimdir. Koleksiyon adi ve arac basligi birlikte yazilir.

`Tutar`, iyzico'ya gonderilecek fiyattir.

```csharp
PrepareCheckoutSummary();
ShowPaymentCheckoutSection();
```

Odeme ozeti hazirlanir ve test karti girilecek checkout paneli acilir.

### 5.2 Test Karti Alanlari Okunur

```csharp
private async void ConfirmPaymentButton_Click(object? sender, RoutedEventArgs e)
```

Kullanici test karti bilgilerini girip odemeyi onayladiginda calisir.

```csharp
var paymentCard = BuildSandboxPaymentCardInput();
```

UI'daki kart alanlari okunur.

```csharp
var cardHolderName = PaymentCardHolderTextBox.Text?.Trim() ?? string.Empty;
var cardNumber = PaymentCardNumberTextBox.Text?.Trim() ?? string.Empty;
var expiryMonth = PaymentExpiryMonthTextBox.Text?.Trim() ?? string.Empty;
var expiryYear = PaymentExpiryYearTextBox.Text?.Trim() ?? string.Empty;
var cvc = PaymentCvvTextBox.Text?.Trim() ?? string.Empty;
```

Kart sahibi, kart numarasi, ay, yil ve CVC textboxlardan okunur.

```csharp
if (string.IsNullOrWhiteSpace(cardHolderName) ||
    string.IsNullOrWhiteSpace(cardNumber) ||
    string.IsNullOrWhiteSpace(expiryMonth) ||
    string.IsNullOrWhiteSpace(expiryYear) ||
    string.IsNullOrWhiteSpace(cvc))
{
    throw new InvalidOperationException("Test kartı alanlarının tamamı zorunlu.");
}
```

Eksik alan varsa iyzico sayfasi acilmadan hata verilir.

```csharp
return new SandboxPaymentCardInput
{
    CardHolderName = cardHolderName,
    CardNumber = cardNumber,
    ExpiryMonth = expiryMonth,
    ExpiryYear = expiryYear,
    Cvc = cvc
};
```

Kart bilgileri tek modelde toplanir.

Model dosyasi: `Models/SandboxPaymentCardInput.cs`

```csharp
public string ExpiryValue => $"{ExpiryMonth}/{ExpiryYear}";
```

iyzico formundaki tarih alani `MM/YY` istedigi icin ay ve yil birlestirilir.

### 5.3 iyzico Checkout Session Olusturulur

```csharp
var session = await _iyzicoPaymentService.InitializeCheckoutAsync(_activeUser, _paymentPreviewItems);
```

Dosya: `Services/Iyzico/IyzicoPaymentService.cs`

```csharp
if (items.Count == 0)
    throw new InvalidOperationException("Odeme icin secili kayit bulunamadi.");
```

Odeme item'i yoksa iyzico istegi baslatilmaz.

```csharp
await _callbackService.StartAsync();
```

iyzico odemesi tamamlaninca donecegi lokal callback server baslatilir.

Dosya: `IyzicoCallbackService.cs`

```csharp
listener.Prefixes.Add($"http://127.0.0.1:{port}/");
listener.Start();
CallbackUrl => $"http://127.0.0.1:{Port}/iyzico/callback";
```

Callback server localhost uzerinde port acar. Varsayilan port `5002`, doluysa sonraki portlari dener. iyzico request'ine verilecek callback URL bu porttan uretilir.

```csharp
var totalAmount = items.Sum(item => item.Tutar);
var conversationId = Guid.NewGuid().ToString("N");
```

Toplam tutar hesaplanir.

`conversationId`, iyzico istegini uygulama tarafinda takip etmek icin benzersiz kimliktir.

```csharp
var request = new CreateCheckoutFormInitializeRequest
{
    Locale = Locale.TR.ToString(),
    ConversationId = conversationId,
    Price = FormatPrice(totalAmount),
    PaidPrice = FormatPrice(totalAmount),
    Currency = Currency.TRY.ToString(),
    BasketId = $"KOL-{user.Id}-{DateTime.UtcNow:yyyyMMddHHmmss}",
    PaymentGroup = PaymentGroup.PRODUCT.ToString(),
    CallbackUrl = _callbackService.CallbackUrl,
    EnabledInstallments = [1],
    Buyer = BuildBuyer(user),
    ShippingAddress = BuildAddress(user),
    BillingAddress = BuildAddress(user),
    BasketItems = items.Select(BuildBasketItem).ToList()
};
```

iyzico checkout initialize request'i hazirlanir.

`Locale`, sayfanin Turkce olmasini ister.

`ConversationId`, bu odeme denemesinin takip ID'sidir.

`Price` ve `PaidPrice`, toplam tutardir. `FormatPrice`, decimal degeri invariant culture ile `0.00` formatina cevirir.

`Currency`, para birimidir. Bu projede `TRY`.

`BasketId`, kullanici ID ve zaman bilgisinden uretilen sepet kimligidir.

`PaymentGroup`, iyzico tarafinda odemenin urun/hizmet odemesi oldugunu belirtir.

`CallbackUrl`, odeme tamamlaninca iyzico'nun donecegi lokal endpoint'tir.

`EnabledInstallments = [1]`, sadece tek cekim aciktir.

`Buyer`, kullanici bilgilerinden olusturulan alici modelidir.

`ShippingAddress` ve `BillingAddress`, sandbox icin sabit adres bilgileridir.

`BasketItems`, odeme itemlerinin iyzico sepet kalemine cevrilmis halidir.

```csharp
var result = await CheckoutFormInitialize.Create(request, BuildOptions());
```

iyzico SDK ile checkout formu olusturulur.

```csharp
private Options BuildOptions()
{
    return new Options
    {
        ApiKey = _settings.ApiKey,
        SecretKey = _settings.SecretKey,
        BaseUrl = _settings.BaseUrl
    };
}
```

SDK'nin kullanacagi api key, secret key ve base URL ayarlardan gelir.

```csharp
if (!string.Equals(result.Status, "success", StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException(result.ErrorMessage ?? "iyzico checkout initialize başarısız.");
```

iyzico basarisiz cevap verirse akis durur.

```csharp
if (string.IsNullOrWhiteSpace(result.Token) || string.IsNullOrWhiteSpace(result.PaymentPageUrl))
    throw new InvalidOperationException("iyzico ödeme sayfası oluşturulamadı.");
```

Token veya odeme sayfasi URL'i yoksa odeme devam edemez.

```csharp
return new IyzicoCheckoutSession
{
    ConversationId = conversationId,
    Token = result.Token,
    PaymentPageUrl = result.PaymentPageUrl,
    TotalAmount = totalAmount
};
```

Uygulamaya geri odeme oturumu dondurulur.

`PaymentPageUrl`, tarayicida acilacak iyzico sandbox sayfasidir.

`Token`, callback ve retrieve islemlerinde kullanilir.

### 5.4 iyzico Odeme Sayfasi Tarayicida Doldurulur

```csharp
ShowBrowserSection();
var baService = CreateBAService();
await baService.CompleteIyzicoSandboxPaymentAsync(session.PaymentPageUrl, paymentCard);
```

Tarayici paneli acilir.

BAService, iyzico odeme sayfasini NativeWebView icinde acar ve kart bilgilerini yazar.

Dosya: `BAService.Payment.cs`

```csharp
if (string.IsNullOrWhiteSpace(paymentPageUrl))
    throw new InvalidOperationException("iyzico ödeme sayfası adresi boş.");

ValidateSandboxCardInput(cardInput);
```

Odeme sayfasi URL'i ve kart bilgileri kontrol edilir.

```csharp
await NavigateAsync(paymentPageUrl);
await WaitForDocumentReadyAsync();
await EnsureJavaScriptHelpersAsync();
```

iyzico sandbox sayfasi acilir, yuklenmesi beklenir ve ortak JS helperlari eklenir.

```csharp
await WaitForScriptTrueAsync(
    """
    (() => {
        const selectors = ['#ccname', '#ccnumber', '#ccexp', '#cccvc'];
        return selectors.every(selector => !!document.querySelector(selector));
    })();
    """,
    TimeSpan.FromSeconds(30));
```

Kart inputlarinin DOM'a gelmesi beklenir.

`#ccname`, kart sahibi.

`#ccnumber`, kart numarasi.

`#ccexp`, son kullanma tarihi.

`#cccvc`, CVC.

```csharp
const tab = document.querySelector('#iyz-tab-credit-card');
tab.click();
```

Kredi karti sekmesi secilir.

```csharp
await WaitForPaymentCardInputsReadyAsync(TimeSpan.FromSeconds(10));
```

Inputlarin gorunur ve disabled olmayan hale gelmesi beklenir.

```csharp
await TypeIntoPaymentFieldAsync("#ccname", cardInput.CardHolderName);
await TypeIntoPaymentFieldAsync("#ccnumber", NormalizeDigits(cardInput.CardNumber));
await TypeIntoPaymentFieldAsync("#ccexp", cardInput.ExpiryValue);
await TypeIntoPaymentFieldAsync("#cccvc", NormalizeDigits(cardInput.Cvc));
```

Her kart alani tek tek doldurulur.

`NormalizeDigits`, kart numarasi ve CVC icindeki bosluk/tire gibi rakam disi karakterleri temizler.

```csharp
private async Task TypeIntoPaymentFieldAsync(string selector, string value)
```

Bu metod verilen selector'a ait inputu bulur ve degeri yazar.

```javascript
const input = document.querySelector(selector);
input.focus();
```

Input bulunur ve odaklanir.

```javascript
const proto = input instanceof HTMLInputElement ? Object.getPrototypeOf(input) : null;
const desc = proto ? Object.getOwnPropertyDescriptor(proto, 'value') : null;
if (desc && desc.set) {
    desc.set.call(input, value);
} else {
    input.value = value;
}
```

Native input value setter'i kullanilir. Bu, iyzico formundaki JS validasyonunun input degerini algilamasina yardim eder.

```javascript
input.dispatchEvent(new Event('input', { bubbles: true }));
input.dispatchEvent(new Event('change', { bubbles: true }));
input.dispatchEvent(new Event('blur', { bubbles: true }));
```

Input yazildi, degisti ve odaktan cikti eventleri gonderilir.

```csharp
await WaitForPaymentFieldValueAsync(selector, value, TimeSpan.FromSeconds(3));
```

Yazilan deger inputa gercekten islenmis mi kontrol edilir.

```javascript
const actual = (input.value || '').trim();
const actualDigits = actual.replace(/\D/g, '');
const expectedDigits = expectedValueDigits;

return actualDigits.endsWith(expectedDigits) || actualDigits === expectedDigits;
```

Input maskesi bosluk ekleyebilir. Bu yuzden rakam disi karakterler silinir ve beklenen rakamlarla karsilastirilir.

```csharp
await WaitForPaymentButtonReadyAsync(TimeSpan.FromSeconds(10));
```

`#iyz-payment-button` gorunur ve tiklanabilir hale gelene kadar beklenir.

```javascript
const button = document.querySelector('#iyz-payment-button');
button.scrollIntoView({ block: 'center', inline: 'nearest' });
button.click();
```

iyzico odeme butonu gorunur alana getirilir ve tiklanir.

### 5.5 Callback Beklenir ve Sonuc iyzico'dan Sorgulanir

```csharp
await _iyzicoPaymentService.WaitForCallbackAsync(session.Token, TimeSpan.FromMinutes(5));
```

Uygulama, iyzico'nun callback endpoint'ine donmesini bekler.

Dosya: `IyzicoCallbackService.cs`

```csharp
var waiter = new TaskCompletionSource<IyzicoCallbackPayload>(TaskCreationOptions.RunContinuationsAsynchronously);

lock (_sync)
{
    _waiters[token] = waiter;
}
```

Her odeme token'i icin bir bekleyici olusturulur. Callback gelince dogru token'in bekleyicisi tamamlanir.

```csharp
using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
timeoutCts.CancelAfter(timeout);
using var registration = timeoutCts.Token.Register(() => waiter.TrySetCanceled(timeoutCts.Token));
```

Callback belirlenen sure icinde gelmezse bekleme iptal edilir.

```csharp
var context = await _listener!.GetContextAsync();
_ = Task.Run(() => HandleContextAsync(context), cancellationToken);
```

HTTP listener gelen callback isteklerini alir ve her istegi ayri task'ta isler.

```csharp
var path = context.Request.Url?.AbsolutePath?.TrimEnd('/') ?? string.Empty;
if (!path.Equals("/iyzico/callback", StringComparison.OrdinalIgnoreCase))
```

Sadece `/iyzico/callback` path'i kabul edilir. Baska path gelirse 404 doner.

```csharp
var payload = await ReadPayloadAsync(context.Request);
```

Query string veya form body icindeki `token`, `status`, `conversationId`, `conversationData`, `paymentId` alanlari okunur.

```csharp
if (_waiters.TryGetValue(payload.Token, out var waiter))
    waiter.TrySetResult(payload);
```

Callback'teki token hangi odeme bekleyicisine aitse o bekleyici tamamlanir.

```csharp
var paymentResult = await _iyzicoPaymentService.RetrievePaymentResultAsync(session.ConversationId, session.Token);
```

Callback geldikten sonra asil odeme sonucu iyzico SDK ile tekrar sorgulanir.

Dosya: `IyzicoPaymentService.cs`

```csharp
var request = new RetrieveCheckoutFormRequest
{
    Locale = Locale.TR.ToString(),
    ConversationId = conversationId,
    Token = token
};

var result = await CheckoutForm.Retrieve(request, BuildOptions());
```

iyzico'ya token ve conversationId ile retrieve istegi atilir.

```csharp
return new IyzicoPaymentResult
{
    ConversationId = conversationId,
    Token = token,
    ReferenceNo = result.PaymentId ?? token,
    Status = result.Status ?? string.Empty,
    PaymentStatus = result.PaymentStatus ?? string.Empty,
    Provider = "iyzico-sandbox",
    CardAssociation = result.CardAssociation,
    LastFourDigits = result.LastFourDigits,
    CardHolderName = null
};
```

iyzico sonucu uygulama modeline cevrilir.

`ReferenceNo`, iyzico payment id varsa onu, yoksa token'i kullanir.

`Status`, iyzico request statusudur.

`PaymentStatus`, odemenin gercek durumudur.

`LastFourDigits`, kartin son 4 hanesidir.

### 5.6 Odeme Basariliysa Veritabanina Yazilir

```csharp
if (!string.Equals(paymentResult.Status, "success", StringComparison.OrdinalIgnoreCase) ||
    !string.Equals(paymentResult.PaymentStatus, "SUCCESS", StringComparison.OrdinalIgnoreCase))
{
    CheckoutStatusTextBlock.Text =
        $"Ödeme tamamlanmadı. Durum: {paymentResult.Status} / {paymentResult.PaymentStatus}";
    return;
}
```

Hem request status `success`, hem payment status `SUCCESS` olmali. Degilse veritabanina basarili odeme yazilmaz.

```csharp
await _databaseService.CreatePaymentsFromSandboxResultAsync(
    _activeUser.Id,
    _paymentPreviewItems,
    paymentResult);
```

Basarili odeme veritabanina kaydedilir.

Dosya: `DatabaseService.Payments.cs`

```csharp
foreach (var item in previewItems)
{
    context.Odemeler.Add(new Odeme
    {
        KullaniciId = kullaniciId,
        KoleksiyonId = item.KoleksiyonId,
        ReferansNo = paymentResult.ReferenceNo,
        KoleksiyonAdi = item.KoleksiyonAdi,
        Tutar = item.Tutar,
        ParaBirimi = "TRY",
        Durum = string.IsNullOrWhiteSpace(paymentResult.PaymentStatus) ? paymentResult.Status : paymentResult.PaymentStatus,
        Saglayici = paymentResult.Provider,
        KartSahibi = paymentResult.CardHolderName,
        KartSon4 = paymentResult.LastFourDigits,
        OdemeTarihi = DateTime.UtcNow
    });
}
```

Her odeme hazirlik item'i icin `Odeme` entity'si olusturulur.

`KullaniciId`, odemenin kullaniciya baglanmasini saglar.

`KoleksiyonId`, odemenin hangi koleksiyondan basladigini saklar.

`ReferansNo`, iyzico odeme referansidir.

`KoleksiyonAdi`, odeme ekraninda gorunen koleksiyon/arac adidir.

`Tutar`, odenen tutardir.

`ParaBirimi`, `TRY` olarak saklanir.

`Durum`, iyzico odeme durumudur.

`Saglayici`, `iyzico-sandbox` olarak saklanir.

`KartSon4`, test kartinin son 4 hanesidir.

`OdemeTarihi`, odemenin uygulamaya kaydedildigi UTC zamandir.

```csharp
await context.SaveChangesAsync();
```

Odeme kaydi veritabanina yazilir.

### 5.7 Odeme Listesi Yenilenir

```csharp
CheckoutStatusTextBlock.Text = "iyzico sandbox ödeme kaydı oluşturuldu.";
ClearCheckoutForm();
ShowPaymentsSection();
await LoadPaymentsAsync();
```

Odeme tamamlaninca test karti formu temizlenir.

Odemeler sekmesi acilir.

Odeme listesi veritabanindan tekrar okunur.

```csharp
private async Task LoadPaymentsAsync()
{
    if (_activeUser is null)
        return;

    var payments = await _databaseService.GetPaymentsAsync(_activeUser.Id);
    PaymentsDataGrid.ItemsSource = null;
    PaymentsDataGrid.ItemsSource = payments;
}
```

Aktif kullanicinin odemeleri okunur ve DataGrid'e yazilir.

Dosya: `DatabaseService.Payments.cs`

```csharp
return await context.Odemeler
    .AsNoTracking()
    .Where(item => item.KullaniciId == kullaniciId)
    .OrderByDescending(item => item.OdemeTarihi)
    .Select(item => new OdemeListItem { ... })
    .ToListAsync();
```

Sadece aktif kullanicinin odemeleri listelenir.

En yeni odeme ustte olacak sekilde siralanir.

Entity yerine DataGrid'e uygun `OdemeListItem` modeli dondurulur.

## 6. Tum Akisin Kisa Sirasi

1. Kullanici register ekraninda email, sifre ve telefon girer.
2. `RegisterButton_Click`, bos alan ve ayni email kontrolu yapar.
3. `SaveOrUpdateUserAsync`, kullaniciyi `kullanicilar` tablosuna kaydeder.
4. Register sonrasi `PerformLoginAsync(..., forceBrowserLogin: true)` calisir.
5. `LoginWithPhoneAsync`, Yolcu360 login sayfasini acar, telefonu yazar ve SMS ekranini bekler.
6. `SmsReceiverService`, MacroDroid'den gelen SMS kodunu yakalar.
7. `FillSmsVerificationCodeAsync`, kodu `#sms_input` alanina yazar ve Dogrula butonuna basar.
8. `WaitForLoginCompletedAsync`, localStorage'da kullanici ve token olusmasini bekler.
9. `SaveSessionAsync`, cookie/localStorage/sessionStorage bilgilerini kullaniciya ait JSON dosyasina kaydeder.
10. Arama ekraninda `SearchButton_Click`, form degerlerinden `SearchFilter` olusturur.
11. `RestoreSessionAsync`, kayitli Yolcu360 session'ini tarayiciya yukler.
12. `OpenYolcu360HomeAsync`, ana sayfayi acar ve popup'i kapatir.
13. `FillPickupLocationAsync`, alis yerini yazar ve autocomplete listesinden en uygun oneriyi secer.
14. `SelectDateRangeAsync`, alis ve donus tarihlerini datepicker uzerinden secer.
15. `SelectTimeAsync`, alis ve donus saatlerini secer.
16. `ClickSearchButtonAsync`, `#search` butonuna basar.
17. `WaitForSearchResultsAsync`, sonuc kartlari gorunene kadar bekler.
18. `ApplyResultFiltersAsync`, vites/yakit filtrelerini sonuc sayfasinda click ile uygular.
19. `ReadSearchResultsAsync`, sonuc kartlarini `SearchResultItem` listesine cevirir.
20. `ResultsDataGrid.ItemsSource`, sonuclari uygulamada listeler.
21. `SaveResultsButton_Click`, `_latestResults` ve `_latestSearchFilter` ile koleksiyon kaydini baslatir.
22. `SaveCollectionAsync`, `koleksiyonlar` tablosuna koleksiyonu ve `araclar` tablosuna araclari yazar.
23. `LoadHistoryAsync`, kaydedilen koleksiyonlari gecmis ekraninda listeler.
24. `OpenSelectedCollectionVehiclesAsync`, secilen koleksiyonun araclarini acar.
25. `CollectionVehiclesDataGrid_SelectionChanged`, odeme yapilacak araci `_selectedVehicle` alanina yazar.
26. `CreatePaymentButton_Click`, secilen arac fiyatindan `OdemeHazirlikItem` olusturur.
27. `ConfirmPaymentButton_Click`, test karti bilgilerini okur.
28. `InitializeCheckoutAsync`, iyzico sandbox checkout session olusturur.
29. `CompleteIyzicoSandboxPaymentAsync`, iyzico sayfasini tarayicida acar, kart bilgilerini yazar ve odeme butonuna basar.
30. `WaitForCallbackAsync`, iyzico callback'ini bekler.
31. `RetrievePaymentResultAsync`, odeme sonucunu iyzico'dan sorgular.
32. `CreatePaymentsFromSandboxResultAsync`, basarili odemeyi `odemeler` tablosuna kaydeder.
33. `LoadPaymentsAsync`, odemeleri DataGrid'de listeler.

## 7. Sunumda Vurgulanacak Ana Mantik

Register sadece yerel kullanici kaydi degildir. Register sonrasi Yolcu360 telefon login akisi de baslar ve basarili olursa oturum dosyaya kaydedilir.

Search sadece HTML parse etmek degildir. Once site uzerinde gercek form doldurulur, autocomplete secilir, tarih/saat secilir, filtreler click ile uygulanir, sonra sonuc kartlari DOM'dan okunur.

Koleksiyon kaydi snapshot mantigindadir. O an gelen araclar `araclar` tablosuna statik olarak yazilir. Koleksiyon satiri ise aramanin kriterlerini saklar.

Odeme gercek banka odemesi degil, iyzico sandbox checkout akisidir. Uygulama iyzico'dan payment page URL alir, tarayicida test kartlarini yazar, callback bekler, sonra sonucu veritabanina kaydeder.

UI tarafinda gorunen her DataGrid dogrudan entity gostermiyor. Genelde veritabanindan DTO/list item modeli uretiliyor ve DataGrid'e o model veriliyor.

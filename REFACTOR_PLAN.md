# Yolcu360 Otomasyon Bakim ve Sadelestirme Plani

Bu dokuman mevcut Avalonia uygulamasinda gereksiz tekrar eden, buyuyen veya ileride hata cikarma ihtimali yuksek kodlari sirali sekilde sadeleştirmek icin hazirlandi. Amac calisan otomasyon akisini bozmadan projeyi daha okunabilir, test edilebilir ve genisletilebilir hale getirmek.

## Ilerleme Durumu

- Tamamlandi: Build'i kiran `DatabaseService` `catch` hatasi giderildi.
- Tamamlandi: Gömülü tarayici otomasyon servisi `MainWindow` icinde tekil hale getirildi. Arama, login ve odeme akislari artik ayni `BAService` instance'ini kullaniyor.
- Tamamlandi: Arama status mesajlari `SetSearchStatus` helper'i uzerinden gecirilmeye baslandi.
- Tamamlandi: Gecmis, arac listesi, odeme ve checkout status mesajlari helper metotlara alindi.
- Tamamlandi: `SearchButton_Click` akisi `TryBuildSearchFilter`, `RunEmbeddedSearchAsync` ve `DisplaySearchResultsAsync` metotlarina ayrildi.
- Tamamlandi: Login/session akisi `TryUseSavedSessionAsync`, `RunPhoneLoginAsync`, `WaitForSmsCodeAsync`, `SubmitSmsCodeAndWaitForLoginAsync` ve `SaveLoginSessionAsync` metotlarina ayrildi.
- Tamamlandi: Session dosya yolu hesaplamasi `AppPaths` helper sinifina tasindi.
- Tamamlandi: DataGrid kolon kurulumunda tekrar eden `DataGridTextColumn` bloklari `AddTextColumn` helper'i ile sadeleştirildi.
- Tamamlandi: Odeme onay akisi `InitializeCheckoutSessionAsync`, `CompleteCheckoutInBrowserAsync` ve `WaitForPaymentResultAsync` metotlarina ayrildi.
- Tamamlandi: Gecmis ekraninda secili koleksiyon, arac listesi ve state temizleme bloklari helper metotlara ayrildi.
- Tamamlandi: PNG export akisi dosya yolu olusturma, koleksiyon/arac verilerini yukleme ve render/kaydetme metotlarina ayrildi.
- Tamamlandi: Ekran gecislerindeki tekrar eden panel/buton gorunurluk kodlari `ShowContentSection` helper'i ile sadeleştirildi.
- Tamamlandi: `BAService` icinde tekrar eden JSON serialize kullanimi `ToJson` helper'i ile merkezi hale getirildi.
- Tamamlandi: Gömülü tarayici sayfa hazirlik/polling beklemeleri isimli sabitlere alindi.
- Tamamlandi: Sonuc filtreleme, takvim, saat ve arama butonu akişlarindaki ham sabit beklemeler isimli sabitlere alindi.
- Tamamlandi: Veritabani servislerinde tekrar eden schema/context olusturma kodlari `CreateContextAsync` helper'i ile merkezi hale getirildi.
- Tamamlandi: Koleksiyon kaydederken kullanici kontrolu `EnsureUserExistsAsync` helper'ina ayrildi.
- Tamamlandi: Arac entity/model donusumleri `ToAracEntity` ve `ToSearchResultItem` mapper metotlarina ayrildi.
- Tamamlandi: `MainWindow.axaml` icindeki ortak stiller `Styles/Controls.axaml` dosyasina tasindi.
- Tamamlandi: Login ve kayit ekranlari `Views/AuthView.axaml` UserControl dosyasina ayrildi.
- Tamamlandi: Arama ekrani `Views/SearchView.axaml` UserControl dosyasina ayrildi.
- Tamamlandi: Gecmis kayitlar ekrani `Views/HistoryView.axaml` UserControl dosyasina ayrildi.
- Tamamlandi: Odeme ve checkout ekranlari `Views/PaymentsView.axaml` UserControl dosyasina ayrildi.
- Tamamlandi: Gömülü tarayici alani `Views/BrowserView.axaml` UserControl dosyasina ayrildi.
- Tamamlandi: `MainWindow.axaml` ana ekran iskeleti seviyesine indirildi.
- Tamamlandi: Odeme formu input doldurma JS'i `SetInputValueAsync` helper'i ile merkezi hale getirildi.
- Tamamlandi: iyzico sekme/buton click islemleri icin ortak `ClickElementAsync`, `ClickButtonByTextAsync` ve `EnsureEmbeddedClickHelperAsync` helper'lari eklendi.
- Tamamlandi: `BAService.SearchForm.cs` alis yeri, tarih secici, saat secici ve arama butonu dosyalarina ayrildi.
- Tamamlandi: PNG export olusturma mantigi `CollectionPngExportService` servisine tasindi.
- Tamamlandi: Auth kodu kontrol baglayicilari, login akisi ve kayit akisi olarak ayri partial dosyalara ayrildi.
- Tamamlandi: `BAService.Auth.cs` telefonla login, SMS dogrulama ve session dosyalarina ayrildi.
- Tamamlandi: `EmbeddedBrowserAutomationService` adi `BAService`, klasoru `Services/BrowserAutomation` olarak kisaltildi.
- Tamamlandi: Search kodu kontrol baglayicilari, arama calistirma ve sonuc kaydetme dosyalarina ayrildi.
- Tamamlandi: History kodu kontrol baglayicilari, veri gosterimi ve koleksiyon aksiyonlari dosyalarina ayrildi.
- Tamamlandi: Payments kodu kontrol baglayicilari, odeme olusturma, checkout ve odeme listesi dosyalarina ayrildi.
- Tamamlandi: `BAService.cs` cekirdek dosyasi config, navigasyon ve DOM helper dosyalarina ayrildi.
- Tamamlandi: PNG export servisi ana servis ve visual builder dosyalarina ayrildi.
- Tamamlandi: Tarih secici akisi `BAService.DatePicker.cs`, takvim navigasyonu `BAService.Calendar.cs` olarak ayrildi.
- Tamamlandi: Telefon login akisi icindeki stealth script ve telefon formatlama helper'lari ayri dosyalara ayrildi.
- Tamamlandi: Alis yeri autocomplete akisi input yazma ve oneri secme dosyalarina ayrildi.
- Tamamlandi: Sonuc akisi filtre uygulama, sonuc bekleme ve sonuc okuma dosyalarina ayrildi.
- Tamamlandi: PNG export visual builder kodu rapor header, koleksiyon kartlari ve tablo builder dosyalarina ayrildi.
- Tamamlandi: Session akisi temizleme, kaydetme ve restore dosyalarina ayrildi.
- Tamamlandi: SMS dogrulama akisi kod kutusu doldurma ve dogrulama butonu tiklama helper'larina ayrildi.
- Siradaki adim: Browser automation JS bloklarinda ortak click/input helper kullanimi artirilabilir; `MainWindow.Ui.cs` tarafinda grid/status/navigation sadeleştirmeleri yapilabilir.

## Mevcut Durum Ozeti

- Uygulama Avalonia ile yaziliyor.
- Gömülü tarayici icin `Avalonia.Controls.WebView` ve `NativeWebView` kullaniliyor.
- Yolcu360 uzerindeki arama, login, SMS dogrulama, sonuc okuma ve odeme akislari `BAService` altinda parcali dosyalara ayrilmis durumda.
- UI kodu `MainWindow` partial dosyalara bolunmus durumda.
- Veritabani islemleri Entity Framework Core ve MySQL uzerinden yapiliyor.
- Iyzico sandbox odeme akisi ayri servislerde tutuluyor.

Kod eskisine gore daha moduler, ancak halen ozellikle UI ve tarayici otomasyonu tarafinda sadeleştirilmesi gereken buyuk bloklar var.

## Oncelik 0: Build Hatalarini Temizleme

### Sorun

`Services/Database/DatabaseService.cs` icinde schema baslatma hatasini yakalayan `catch` blogunda `ex.Message` kullaniliyor, fakat `ex` degiskeni tanimli degildi.

### Cozum

`catch` blogu `catch (Exception ex)` haline getirildi.

### Neden Oncelikli?

Build kirmaya devam eden bir hata varken refactor yapmak risklidir. Once proje her zaman derlenebilir duruma getirilmeli.

## Oncelik 1: MainWindow.axaml Dosyasini Parcalara Ayirma

### Sorun

`MainWindow.axaml` 1000 satirin uzerinde. Login, kayit, arama, tarayici, gecmis kayitlar, odeme listesi ve checkout ekranlari ayni XAML icinde duruyor.

Bu durum su sorunlari olusturuyor:

- Tasarim degisikligi yaparken alakasiz ekranlari bozma riski artiyor.
- XAML hatalarini bulmak zorlasiyor.
- Ayni style veya layout kaliplari farkli yerlerde tekrar ediyor.
- Kod-behind tarafinda hangi kontrolun hangi ekrana ait oldugu belirsizlesiyor.

### Hedef Yapi

Asagidaki `UserControl` dosyalari olusturuldu:

- `Views/AuthView.axaml`
- `Views/SearchView.axaml`
- `Views/BrowserView.axaml`
- `Views/HistoryView.axaml`
- `Views/PaymentsView.axaml`

Ortak style dosyalari:

- `Styles/Controls.axaml`

### Uygulama Sirasi

1. Tamamlandi: Login ve kayit ekranlari `AuthView` icine alindi.
2. Tamamlandi: Arama ekrani `SearchView` icine alindi.
3. Tamamlandi: Gecmis ekrani `HistoryView` icine alindi.
4. Tamamlandi: Odeme ve checkout ekranlari `PaymentsView` icine alindi.
5. Tamamlandi: Tarayici bolumu `BrowserView` icine alindi.

Bu asamada `MainWindow.axaml` sadece ana iskelet, ust navigasyon ve view yerlesimi gorevini tasiyor.

## Oncelik 2: BAService Icindeki JS Tekrarlarini Azaltma

### Sorun

`BAService.SearchForm.cs` cok buyuk ve cok sayida inline JavaScript barindiriyor. Ayni mantik birden fazla yerde tekrar ediyor:

- Element gorunur mu kontrolu
- Element merkezine click atma
- Pointer/mouse event zinciri dispatch etme
- Input degerini set edip `input/change` eventleri firlatma
- Dropdown onerilerini bekleme
- Sabit `Task.Delay` kullanimi

Bu tekrarlar yuzunden kucuk selector veya event degisikligi farkli yerlerde unutulabiliyor. Daha once alis yeri, tarih ve saat secme sorunlarinin bu kadar cabuk bozulmasinin ana sebebi bu.

### Onerilen Helper Metotlar

`BAService.cs` icine veya ayri bir `EmbeddedDomAutomation.cs` dosyasina su metotlar alinabilir:

```csharp
private Task<bool> WaitForVisibleAsync(string selector, TimeSpan timeout);
private Task<bool> ClickElementAsync(string selector);
private Task<bool> ClickElementByTextAsync(string selector, string expectedText);
private Task FillInputAsync(string selector, string value);
private Task FillInputHumanLikeAsync(string selector, string value, int minDelayMs, int maxDelayMs);
private Task<bool> WaitUntilAsync(Func<Task<bool>> condition, TimeSpan timeout, int intervalMs = 250);
```

### Onerilen JS Sabitleri

Tekrar eden JS fonksiyonlari tek yerde tutulabilir:

```csharp
private const string VisibleFunctionScript = """
const visible = el => {
    if (!el) return false;
    const rect = el.getBoundingClientRect();
    const style = window.getComputedStyle(el);
    return rect.width > 0 &&
        rect.height > 0 &&
        style.display !== 'none' &&
        style.visibility !== 'hidden';
};
""";
```

Click icin de tek helper:

```csharp
private const string ClickFunctionScript = """
const clickCenter = el => {
    const rect = el.getBoundingClientRect();
    const x = rect.left + rect.width / 2;
    const y = rect.top + rect.height / 2;
    for (const type of ['pointerdown', 'mousedown', 'mouseup', 'click']) {
        el.dispatchEvent(new MouseEvent(type, {
            bubbles: true,
            cancelable: true,
            clientX: x,
            clientY: y,
            view: window
        }));
    }
};
""";
```

### Beklenen Kazanc

- Alis yeri secimi tek yerden duzeltilir.
- Tarih/saat secimi tek click mekanizmasini kullanir.
- Safari/WebKit kaynakli selector farklari daha kontrollu yonetilir.
- Debug loglari ayni formatta gelir.

## Oncelik 3: Search Akisini Kucuk Metotlara Bolme

### Sorun

Eski tek dosyali Search akisi icindeki `SearchButton_Click` birden fazla sorumluluk tasiyordu:

- UI buton durumunu ayarliyor.
- Tarih parse ediyor.
- SearchFilter olusturuyor.
- Login kontrolu yapiyor.
- Tarayici oturumunu restore ediyor.
- Arama formunu dolduruyor.
- Filtreleri uyguluyor.
- Sonuclari grid'e basiyor.
- Hata/status yonetiyor.

### Onerilen Parcalama

```csharp
private bool TryBuildSearchFilter(out SearchFilter filter);
private Task<List<SearchResultItem>> RunSearchAsync(SearchFilter filter);
private void DisplaySearchResults(List<SearchResultItem> results);
private Task RestoreBrowserSessionIfNeededAsync(BAService baService);
```

### Beklenen Kazanc

Arama sonucunun gelmemesi, filtre uygulanmamasi veya grid'e basilmamasi gibi sorunlar tek tek izole edilir. Simdiki haliyle tek handler icinde hata kaynagi bulmak zor.

### Mevcut Durum

Bu akis su dosyalara ayrildi:

- `MainWindow.SearchControls.cs`
- `MainWindow.SearchRun.cs`
- `MainWindow.SearchSave.cs`

## Oncelik 4: Login ve Session Akisini Servise Ayirma

### Sorun

`MainWindow.Auth.cs` icindeki `PerformLoginAsync` cok fazla is yapiyor:

- Kullanici bilgilerini kontrol ediyor.
- Session dosyasini buluyor.
- Session varsa ana ekrana geciyor.
- Session yoksa tarayici aciyor.
- Telefon ile Yolcu360 girisi yaptiriyor.
- SMS kodunu bekliyor.
- SMS kodunu sayfaya yaziyor.
- Login tamamlaninca session kaydediyor.
- Kullanici kaydini guncelliyor.
- UI ekranlarini degistiriyor.

### Onerilen Yapi

Yeni bir servis:

```csharp
Services/Auth/AuthWorkflowService.cs
```

Ana metotlar:

```csharp
Task<AppUser?> ValidateLocalUserAsync(string email, string password);
Task<bool> TryUseSavedSessionAsync(AppUser user);
Task RunPhoneLoginAsync(AppUser user);
Task SaveBrowserSessionAsync(AppUser user);
```

UI sadece status ve ekran gecisleriyle ilgilenmeli.

### Beklenen Kazanc

Session saklama, SMS bekleme ve tarayici login adimlari birbirinden ayrilir. Captcha veya SMS tarafinda sorun oldugunda UI koduna dokunmadan debug yapilir.

## Oncelik 5: BAService Nesnesini Tekil Kullanma

### Sorun

`CreateBAService()` her cagrildiginda yeni servis uretiyor ve `ProgressChanged` eventini yeniden bagliyor.

### Onerilen Cozum

`MainWindow.axaml.cs` icinde bir field tutulabilir:

```csharp
private BAService? _baService;
```

Lazy init:

```csharp
private BAService GetBAService()
{
    if (_baService is not null)
        return _baService;

    _baService = new BAService(EmbeddedBrowser);
    _baService.ProgressChanged += OnEmbeddedBrowserProgressChanged;
    return _baService;
}
```

### Beklenen Kazanc

- Event baglantilari kontrol altinda olur.
- Servis state tutarsa kaybolmaz.
- Arama, login ve odeme ayni browser servisinden ilerler.

## Oncelik 6: Status Mesajlarini Merkezi Hale Getirme

### Sorun

Farkli dosyalarda dogrudan su tarz atamalar var:

```csharp
SearchStatusTextBlock.Text = "...";
HistoryStatusTextBlock.Text = "...";
CheckoutStatusTextBlock.Text = "...";
PaymentsStatusTextBlock.Text = "...";
```

Bu durum status mesajlarinin dagilmasina ve bazen yanlis ekranda gorunmesine neden olabilir.

### Onerilen Cozum

`MainWindow.Ui.cs` icine helper metotlar:

```csharp
private void SetSearchStatus(string message);
private void SetHistoryStatus(string message);
private void SetCheckoutStatus(string message);
private void SetPaymentsStatus(string message);
```

Eger ileride log paneli eklenirse bu metotlar ayni anda hem UI'a hem log alanina yazabilir.

## Oncelik 7: Sabit Beklemeleri Isimli Hale Getirme

### Sorun

Tarayici otomasyonunda cok sayida `Task.Delay(...)` var. Bunlar bazen gerekli, ancak hangi beklemenin ne icin oldugu belli degil.

### Onerilen Cozum

Sabitler:

```csharp
private static readonly TimeSpan PageHydrationDelay = TimeSpan.FromMilliseconds(2500);
private static readonly TimeSpan AfterClickDelay = TimeSpan.FromMilliseconds(300);
private static readonly TimeSpan ResultsExtraWait = TimeSpan.FromMilliseconds(1500);
```

Daha iyi cozum:

Sadece gercekten gerekli yerlerde delay, diger yerlerde polling:

```csharp
await WaitUntilAsync(() => ElementExistsAsync(selector), TimeSpan.FromSeconds(10));
```

### Beklenen Kazanc

Site yavasladiginda veya hizlandiginda tek tek delay aramak gerekmez.

## Oncelik 8: Veritabani Modelini Fiyat Takibine Hazirlama

### Sorun

`Arac` modelinde fiyatlar string tutuluyor. Bu ekranda gostermek icin yeterli ama fiyat takibi, fiyat dususu bildirimi veya karsilastirma icin zayif.

### Onerilen Alanlar

`araclar` tablosuna:

```csharp
decimal? FiyatTutar
decimal? GunlukFiyatTutar
string ParaBirimi
```

Ek tablo:

```csharp
FiyatGecmisi
```

Onerilen kolonlar:

- `Id`
- `AracId`
- `KoleksiyonId`
- `FiyatTutar`
- `GunlukFiyatTutar`
- `ParaBirimi`
- `KontrolTarihi`
- `KaynakUrl`

### Beklenen Kazanc

Sonradan fiyat takip ozelligi eklemek kolaylasir.

## Oncelik 9: Sifre Saklama Mantigini Guvenli Hale Getirme

### Sorun

Kullanici sifresi veritabaninda duz metin olarak tutuluyor.

### Onerilen Cozum

Basit ve yeterli bir iyilestirme:

- `Password` yerine `PasswordHash`
- `PasswordSalt`
- Kayitta hash uretme
- Giriste hash karsilastirma

### Not

Bu uygulama staj/demo kapsaminda olsa bile teknik raporda duz metin sifre saklamak zayif gorunur.

## Oncelik 10: EnsureCreated Yerine Kontrollu Schema Yonetimi

### Sorun

EF Core `EnsureCreatedAsync` ilk kurulum icin kolay, ancak tablo yapisi degistikce mevcut veritabanini guncellemekte yetersiz kalir.

### Onerilen Cozum

Iki secenek var:

1. EF Core migrations kullanmak.
2. Basit kalmak istenirse `EnsureSchemaUpToDateAsync` gibi kontrollu SQL migration metodu yazmak.

Bu projede tablo adlari Turkce ve Task.md ile uyumlu tutuldugu icin migration dosyalari daha temiz olur.

## Oncelik 11: Debug/Test Kalintilarini Ayirma

### Sorun

`NativeWebViewTestButton` kodda duruyor. UI tarafinda gizleniyor ama production akista gerekli degilse kafa karistirir.

### Onerilen Cozum

Ya tamamen kaldirilir ya da debug kosuluna baglanir:

```csharp
#if DEBUG
NativeWebViewTestButton.IsVisible = true;
#else
NativeWebViewTestButton.IsVisible = false;
#endif
```

## Onerilen Uygulama Sirasi

1. Build hatalarini temizle.
2. XAML'i once auth ekranlarindan baslayarak parcala.
3. `BAService` icindeki JS helper tekrarlarini azalt.
4. Search handler'i kucuk metotlara bol.
5. Login/session akisini servis haline getir.
6. Status mesajlarini merkezi helperlara tasi.
7. Sabit beklemeleri isimli constant/polling yapisina al.
8. Veritabani fiyat alanlarini numeric hale getir.
9. Sifre saklama mantigini hash yapisina gecir.
10. Debug/test kontrollerini ayir.

## Dikkat Edilmesi Gerekenler

- Alis yeri secimi, tarih secimi ve saat secimi hassas kisimlar. Bu kisimlarda refactor yaparken davranis ayni kalmali.
- Her refactor adimindan sonra uygulama manuel test edilmeli.
- Tek seferde buyuk temizlik yapilmamali.
- Once kodun dis davranisi korunmali, sonra ic yapi sadeleştirilmeli.
- Veritabani degisiklikleri migration veya kontrollu SQL ile yapilmali; mevcut tablolar rastgele silinmemeli.

## Kisa Sonuc

Projede en acil teknik borc build hatasiydi ve bu giderildi. En buyuk yapisal borc ise UI'in tek buyuk XAML dosyasinda toplanmasi ve tarayici otomasyonundaki tekrar eden JavaScript bloklari. Bunlar sirayla temizlenirse uygulamanin bozulma riski ciddi sekilde azalir.

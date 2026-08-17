# Yolcu360 Otomasyon

Yolcu360 Otomasyon, Avalonia ile geliştirilmiş masaüstü bir araç kiralama otomasyon uygulamasıdır. Uygulama kullanıcı hesabı, SMS doğrulama, Yolcu360 araç arama formu otomasyonu, sonuç listeleme, koleksiyon kaydetme, PNG dışa aktarma ve iyzico sandbox ödeme işlemlerini tek arayüzde toplar.

## İçindekiler

- [Genel Amaç](#genel-amaç)
- [Teknolojiler](#teknolojiler)
- [Temel Özellikler](#temel-özellikler)
- [Proje Yapısı](#proje-yapısı)
- [Kurulum](#kurulum)
- [Konfigürasyon](#konfigürasyon)
- [Çalıştırma](#çalıştırma)
- [Kullanım Akışı](#kullanım-akışı)
- [Mimari Akışlar](#mimari-akışlar)
- [Veritabanı Yapısı](#veritabanı-yapısı)
- [SMS Doğrulama](#sms-doğrulama)
- [Ödeme Sistemi](#ödeme-sistemi)
- [Önemli Dosyalar](#önemli-dosyalar)
- [Sunumda Anlatılabilecek Noktalar](#sunumda-anlatılabilecek-noktalar)
- [Sorun Giderme](#sorun-giderme)

## Genel Amaç

Bu proje, Yolcu360 sitesi üzerinde araç kiralama aramasını otomatikleştirmek ve sonuçları masaüstü uygulama içinde yönetilebilir hale getirmek için hazırlanmıştır.

Uygulama şunları yapar:

- Kullanıcıyı uygulama hesabı ile kaydeder veya giriş yaptırır.
- Kullanıcının telefon numarasıyla Yolcu360 oturumunu açar.
- SMS doğrulama kodunu MacroDroid üzerinden alır.
- Yolcu360 araç kiralama formunu gömülü web görünümü üzerinden doldurur.
- Alış yeri autocomplete önerisini seçer ve seçimin uygulandığını doğrular.
- Tarih, saat, vites ve yakıt kriterlerini uygular.
- Sonuçları Avalonia `DataGrid` içinde listeler.
- Seçilen sonuçları özel isimle koleksiyon olarak kaydeder.
- Koleksiyonları geçmiş sayfasında gösterir, siler ve PNG olarak indirir.
- Seçilen araç için iyzico sandbox üzerinden direkt ödeme isteği gönderir.

## Teknolojiler

Proje `.NET 8` ve Avalonia tabanlıdır.

Kullanılan ana paketler:

- `Avalonia`
- `Avalonia.Desktop`
- `Avalonia.Controls.DataGrid`
- `Avalonia.Controls.WebView`
- `Avalonia.Themes.Fluent`
- `Microsoft.EntityFrameworkCore`
- `Pomelo.EntityFrameworkCore.MySql`
- `MySqlConnector`
- `Iyzipay`

Ana platform hedefi:

- macOS üzerinde Avalonia masaüstü uygulaması.
- MySQL veritabanı.
- Native WebView ile Yolcu360 sayfası otomasyonu.

## Temel Özellikler

### Kullanıcı İşlemleri

- Email ve şifre ile uygulamaya giriş.
- Email, şifre ve telefon numarası ile kayıt.
- Kullanıcıya bağlı telefon numarası ile Yolcu360 SMS giriş işlemi.
- Yolcu360 oturum bilgilerinin `sessions` klasöründe JSON olarak saklanması.
- Session varsa tekrar SMS girişi yapmadan ana ekrana geçiş.

### Araç Arama

- Alış yeri yazma.
- Yolcu360 autocomplete önerisini seçme.
- Seçimin uygulanmasını bekleme.
- Alış ve dönüş tarihlerini Yolcu360 tarih seçicisinden seçme.
- Alış ve dönüş saatlerini seçme.
- Vites ve yakıt filtrelerini sonuç sayfasında click ile uygulama.
- Sonuçları uygulama içindeki `DataGrid` alanında listeleme.
- Sonuç yoksa kullanıcıya sonuç bulunamadı bilgisi gösterme.

### Koleksiyonlar

- Arama sonuçlarını özel isimle kaydetme.
- Koleksiyon içinde gelen araçları saklama.
- Geçmiş kayıtları listeleme.
- Koleksiyon silme.
- Koleksiyonları PNG olarak dışa aktarma.
- Dinamik koleksiyon güncelleme özelliği için ayrı servis yapısı.

### Ödeme

- Seçilen koleksiyondaki seçili araç için ödeme hazırlama.
- Test kartı bilgilerini uygulamada alma.
- iyzico sandbox API’ye direkt ödeme isteği gönderme.
- Başarılı ödeme sonucunu veritabanına kaydetme.
- Ödeme başarılıysa `Ödemeler` sayfasına yönlendirme.

## Proje Yapısı

```text
Yolcu360Otomasyon/
├── Configuration/
│   ├── AppSettings.cs
│   └── IyzicoSettings.cs
├── Data/
│   └── AppDbContext.cs
├── MainWindow/
│   ├── MainWindow.Auth.cs
│   ├── MainWindow.Search.cs
│   ├── MainWindow.History.cs
│   ├── MainWindow.Payments.cs
│   ├── MainWindow.Flight.cs
│   ├── MainWindow.Export.cs
│   └── MainWindow.Ui.cs
├── Models/
├── Services/
│   ├── BrowserAutomation/
│   ├── Collections/
│   ├── Database/
│   ├── Iyzico/
│   ├── Locations/
│   └── SmsReceiver/
├── html/
├── md/
├── sessions/
├── MainWindow.axaml
├── MainWindow.axaml.cs
├── Program.cs
└── Yolcu360Otomasyon.csproj
```

### MainWindow Klasörü

`MainWindow` partial class olarak ayrılmıştır. Amaç tek dosyada binlerce satır kod tutmak yerine ekran olaylarını konu bazlı ayırmaktır.

- `MainWindow.Auth.cs`: Giriş, kayıt ve oturum akışı.
- `MainWindow.Search.cs`: Araç arama ekranı olayları.
- `MainWindow.History.cs`: Koleksiyon ve geçmiş kayıt işlemleri.
- `MainWindow.Payments.cs`: Ödeme hazırlama ve ödeme başlatma işlemleri.
- `MainWindow.Export.cs`: PNG dışa aktarma.
- `MainWindow.Ui.cs`: Panel göster/gizle, DataGrid kolon ayarları ve UI yardımcıları.
- `MainWindow.Flight.cs`: Uçak bileti tarafı için ayrılmış ekstra özellik kodları.

### Services Klasörü

İş mantığı bu klasörde tutulur.

- `BrowserAutomation/`: Native WebView üzerinde Yolcu360 sayfasını kontrol eder.
- `Database/`: EF Core ile veritabanı işlemlerini yapar.
- `Iyzico/`: iyzico sandbox ödeme işlemlerini yönetir.
- `SmsReceiver/`: MacroDroid’den gelen SMS doğrulama kodlarını HTTP üzerinden alır.
- `Locations/`: Yolcu360 uyumlu lokasyon önerilerini uygulama tarafında gösterir.
- `Collections/`: Kaydedilmiş koleksiyonların güncellenmesi gibi ek işlemler.

## Kurulum

Gereksinimler:

- .NET 8 SDK
- MySQL
- macOS üzerinde çalıştırma ortamı
- MacroDroid kurulu Android cihaz
- iyzico sandbox API bilgileri

Bağımlılıkları geri yüklemek için:

```bash
dotnet restore
```

Derlemek için:

```bash
dotnet build
```

Çalıştırmak için:

```bash
dotnet run
```

## Konfigürasyon

Uygulama ayarları `Configuration/AppSettings.cs` üzerinden okunur.

### Veritabanı Connection String

Connection string şu sırayla aranır:

1. `.NET user-secrets`
2. `YOLCU360_CONNECTION_STRING` environment variable
3. `key.json`
4. `appsettings.json`
5. `Others/key.json`

Beklenen key:

```json
{
  "ConnectionStrings": {
    "Yolcu360Database": "server=localhost;port=3306;database=yolcu360db;user=root;password=..."
  }
}
```

Environment variable alternatifi:

```bash
export YOLCU360_CONNECTION_STRING="server=localhost;port=3306;database=yolcu360db;user=root;password=..."
```

### iyzico Sandbox Ayarları

iyzico bilgileri şu keylerden okunur:

```json
{
  "IYZ_API_KEY": "...",
  "IYZ_SECURITY_KEY": "..."
}
```

Environment variable alternatifi:

```bash
export IYZ_API_KEY="..."
export IYZ_SECURITY_KEY="..."
```

Base URL kod içinde sandbox olarak ayarlanmıştır:

```text
https://sandbox-api.iyzipay.com
```

## Çalıştırma

Proje kökünde:

```bash
dotnet run
```

İlk açılışta:

1. Veritabanı şeması kontrol edilir.
2. SMS listener başlatılır.
3. Kullanıcı login ekranı açılır.
4. Session varsa giriş sonrası Yolcu360 SMS akışı tekrar çalıştırılmadan ana sayfaya geçilebilir.

## Kullanım Akışı

### 1. Kayıt

Kayıt ekranında:

- Email
- Şifre
- Telefon numarası

girilir.

Kayıt sonrası uygulama Yolcu360 tarafında telefon numarası ile giriş başlatır. SMS kodu MacroDroid üzerinden uygulamaya gelirse kod otomatik yazılır ve doğrulama tamamlanır.

### 2. Giriş

Giriş ekranında:

- Email
- Şifre

girilir.

Bu email veritabanında kayıtlı telefon numarasıyla eşleşir. Eğer kullanıcıya ait session dosyası varsa uygulama doğrudan ana ekrana geçer. Session yoksa Yolcu360 telefon girişi başlatılır.

### 3. Araç Arama

Araç kiralama ekranında:

- Alış yeri
- Opsiyonel bırakış yeri
- Alış tarihi
- Dönüş tarihi
- Alış saati
- Dönüş saati
- Vites filtresi
- Yakıt filtresi

seçilir.

Arama başlatılınca `BAService` WebView üzerindeki Yolcu360 formunu doldurur.

### 4. Sonuç Listeleme

Yolcu360 sonuç sayfası yüklendikten sonra:

- araç kartları DOM’dan okunur,
- `SearchResultItem` modeline çevrilir,
- Avalonia `DataGrid` içinde gösterilir.

Sonuç yoksa DataGrid alanında sonuç bulunamadı bilgisi gösterilir.

### 5. Koleksiyon Kaydetme

Arama sonuçları özel isimle kaydedilir. Kaydedilen veri:

- koleksiyon bilgisi,
- arama kriterleri,
- sonuçta gelen araçlar

olarak veritabanına yazılır.

### 6. Geçmiş Kayıtlar

Geçmiş sayfasında:

- koleksiyonlar listelenir,
- koleksiyon silinebilir,
- koleksiyon PNG olarak indirilebilir,
- seçilen koleksiyon için ödeme başlatılabilir.

### 7. Ödeme

Ödeme ekranında test kartı bilgileri girilir.

Uygulama artık checkout sayfası açmaz. Direkt olarak iyzico sandbox API’ye ödeme isteği gönderir.

Başarılı cevap gelirse:

- ödeme `odemeler` tablosuna kaydedilir,
- kullanıcı `Ödemeler` sayfasına yönlendirilir,
- ödeme listesi yenilenir.

## Mimari Akışlar

### Auth Akışı

Başlangıç noktası:

```text
MainWindow.Auth.cs
```

Genel akış:

```text
Login/Register Button
    -> MainWindow.Auth.cs
    -> DatabaseService
    -> BAService.Auth.cs
    -> SmsReceiverService
    -> Session JSON
    -> MainView
```

Sorumluluk dağılımı:

- `MainWindow.Auth.cs`: UI olayını alır, kullanıcıyı yönlendirir.
- `DatabaseService`: kullanıcıyı kaydeder veya getirir.
- `BAService.Auth.cs`: Yolcu360 sitesinde telefon girişini yapar.
- `SmsReceiverService`: SMS kodunu HTTP üzerinden alır.
- Session dosyası: Yolcu360 cookie/localStorage bilgisini saklar.

### Araç Arama Akışı

Başlangıç noktası:

```text
MainWindow.Search.cs
```

Genel akış:

```text
Search Button
    -> SearchFilter oluşturulur
    -> BAService.OpenYolcu360HomeAsync
    -> BAService.FillSearchFormAsync
    -> BAService.ClickSearchButtonAsync
    -> BAService.WaitForSearchResultsAsync
    -> BAService.ApplyResultFiltersAsync
    -> BAService.ReadSearchResultsAsync
    -> MainWindow DataGrid
```

Önemli kontrol noktaları:

- Alış yeri yazıldıktan sonra autocomplete önerisi beklenir.
- Öneriye tıklandıktan sonra seçimin uygulandığı doğrulanır.
- Tarih seçici açılmadan tarih seçme yapılmaz.
- Saat dropdown değeri seçildikten sonra UI üzerinde uygulandığı kontrol edilir.
- Sonuç kartları görünmeden veri okuma yapılmaz.

### Koleksiyon Akışı

Başlangıç noktası:

```text
MainWindow.History.cs
```

Genel akış:

```text
Save Collection
    -> DatabaseService.SaveCollectionAsync
    -> koleksiyonlar tablosu
    -> araclar tablosu

History Tab
    -> DatabaseService.GetCollectionsAsync
    -> DataGrid
```

### Ödeme Akışı

Başlangıç noktası:

```text
MainWindow.Payments.cs
```

Genel akış:

```text
Seçilen Araç İçin Ödeme
    -> ödeme özeti hazırlanır
    -> ödeme formu açılır
    -> kart bilgileri alınır
    -> IyzicoPaymentService.CreateDirectPaymentAsync
    -> iyzico sandbox API
    -> DatabaseService.CreatePaymentsFromSandboxResultAsync
    -> Ödemeler sayfası
```

Bu akışta artık:

- callback URL yoktur,
- listener yoktur,
- iyzico checkout sayfası açılmaz,
- tarayıcıdan ödeme formu doldurulmaz.

## Veritabanı Yapısı

Veritabanı EF Core ile yönetilir. Ana tablo isimleri Türkçe tutulmuştur.

### kullanicilar

Uygulama kullanıcılarını tutar.

Önemli alanlar:

- `Email`
- `Password`
- `PhoneNumber`
- `SessionStatePath`
- `CreatedAt`
- `UpdatedAt`

### koleksiyonlar

Kaydedilen arama kayıtlarını tutar.

Önemli alanlar:

- `OzelAd`
- `AlisYeri`
- `AlisTarihi`
- `DonusTarihi`
- `AlisSaati`
- `DonusSaati`
- `SecilenVitesFiltresi`
- `SecilenYakitFiltresi`
- `OlusturmaTarihi`
- `KullaniciId`

### araclar

Koleksiyon içindeki araç sonuçlarını tutar.

Önemli alanlar:

- `Baslik`
- `AltBaslik`
- `Fiyat`
- `GunlukFiyat`
- `Vites`
- `Yakit`
- `Sirket`
- `TeslimBilgisi`
- `IslemMetni`
- `Baglanti`
- `KoleksiyonId`

### odemeler

iyzico sandbox ödeme kayıtlarını tutar.

Önemli alanlar:

- `ReferansNo`
- `KoleksiyonAdi`
- `Tutar`
- `ParaBirimi`
- `Durum`
- `Saglayici`
- `KartSahibi`
- `KartSon4`
- `OdemeTarihi`
- `KullaniciId`
- `KoleksiyonId`

## SMS Doğrulama

SMS doğrulama için Android cihazda MacroDroid kullanılır.

Uygulama tarafında:

```text
SmsReceiverService
```

HTTP listener açar.

Varsayılan port:

```text
5001
```

MacroDroid URL formatı:

```text
http://BILGISAYAR_IP:5001/sms?message={sms_message}
```

Örnek:

```text
http://192.168.1.29:5001/sms?message={sms_message}
```

SMS geldiğinde:

1. MacroDroid SMS metnini uygulamaya gönderir.
2. `SmsReceiverService` metinden 4-8 haneli kodu regex ile yakalar.
3. Bekleyen login akışına kod iletilir.
4. `BAService.Auth.cs` kodu Yolcu360 SMS doğrulama alanına yazar.

## Ödeme Sistemi

Ödeme işlemleri:

```text
Services/Iyzico/IyzicoPaymentService.cs
```

dosyasında yapılır.

Mevcut ödeme modeli:

- `CreatePaymentRequest` kullanılır.
- Kart bilgileri uygulama içinden alınır.
- iyzico sandbox API’ye direkt istek gönderilir.
- `Status == success` başarılı kabul edilir.
- Başarılı sonuç `odemeler` tablosuna kaydedilir.

Ödeme ekranı:

```text
MainWindow/MainWindow.Payments.cs
```

Ödeme kaydetme:

```text
Services/Database/DatabaseService.Payments.cs
```

## Önemli Dosyalar

### Uygulama Başlangıcı

```text
Program.cs
App.axaml.cs
MainWindow.axaml.cs
```

### UI

```text
MainWindow.axaml
MainWindow/MainWindow.Ui.cs
```

### Auth

```text
MainWindow/MainWindow.Auth.cs
Services/BrowserAutomation/BAService.Auth.cs
Services/SmsReceiver/SmsReceiverService.cs
```

### Arama

```text
MainWindow/MainWindow.Search.cs
Services/BrowserAutomation/BAService.SearchForm.cs
Services/BrowserAutomation/BAService.SearchResults.cs
Models/SearchFilter.cs
Models/SearchResultItem.cs
```

### Geçmiş ve Koleksiyon

```text
MainWindow/MainWindow.History.cs
Services/Database/DatabaseService.Collections.cs
Services/Collections/DynamicCollectionService.cs
Models/Koleksiyon.cs
Models/Arac.cs
Models/KoleksiyonListItem.cs
```

### Ödeme

```text
MainWindow/MainWindow.Payments.cs
Services/Iyzico/IyzicoPaymentService.cs
Services/Database/DatabaseService.Payments.cs
Models/SandboxPaymentCardInput.cs
Models/IyzicoPaymentResult.cs
Models/Odeme.cs
Models/OdemeListItem.cs
```

### Ek Teknik Dokümanlar

`md/` klasöründe daha ayrıntılı teknik notlar bulunur:

- `md/FLOW.md`
- `md/FUNCTION_INDEX.md`
- `md/SEARCH_CODE_WALKTHROUGH.md`
- `md/SEARCH_FORM_SCRIPTS.md`
- `md/RECAPTCHA_ANALYSIS.md`
- `md/Task.md`
- `md/plan.md`

## Sunumda Anlatılabilecek Noktalar

### 1. Neden Avalonia?

Uygulama macOS üzerinde geliştirildiği için WinForms veya CefSharp uygun değildir. Avalonia çapraz platform masaüstü uygulaması geliştirmek için kullanılmıştır.

### 2. Neden Native WebView?

Yolcu360 işlemlerini kullanıcıya göstermek ve DOM üzerinde script çalıştırmak için Avalonia WebView kullanılmıştır.

### 3. Neden `WaitUntilAsync`?

Sabit bekleme süreleri yerine sayfanın gerçekten hazır olup olmadığı kontrol edilir. Örneğin:

- autocomplete önerisi geldi mi,
- tarih menüsü açıldı mı,
- saat değeri uygulandı mı,
- sonuç kartları göründü mü.

Bu yaklaşım, `Task.Delay` ile kör beklemekten daha kontrollüdür.

### 4. Neden Session Saklanıyor?

Yolcu360 login işlemi SMS doğrulama gerektirir. Kullanıcı başarılı giriş yaptıktan sonra cookie/localStorage bilgisi dosyaya kaydedilir. Böylece sonraki girişlerde aynı kullanıcı için tekrar SMS doğrulama yapılmadan oturum restore edilebilir.

### 5. Neden EF Core?

Veritabanı işlemleri doğrudan SQL stringleri yerine model tabanlı yönetilir. Bu sayede:

- tablo-model ilişkileri daha net olur,
- sorgular tip güvenli yazılır,
- kayıt/silme/listeleme işlemleri daha okunabilir olur.

### 6. Neden Direkt iyzico Payment API?

İlk yaklaşım checkout sayfası, callback URL ve listener gerektiriyordu. Son haliyle uygulama direkt sandbox ödeme isteği gönderir. Bu daha sade ve sunum için daha anlaşılırdır.

## Sorun Giderme

### SMS Kodu Gelmiyor

Kontrol edilecekler:

- Bilgisayar ve telefon aynı ağda mı?
- Uygulama 5001 portunu açabildi mi?
- MacroDroid URL doğru mu?
- IP adresi değişti mi?
- URL formatı şu şekilde mi?

```text
http://IP_ADRESI:5001/sms?message={sms_message}
```

### Port Kullanımda

Hata örneği:

```text
Address already in use
```

Çözüm:

- Aynı portu kullanan eski uygulama instance'ını kapatın.
- Uygulamayı yeniden başlatın.
- MacroDroid portu sabit olduğu için uygulama otomatik farklı porta geçmez.

### MySQL Bağlantı Hatası

Kontrol edilecekler:

- MySQL çalışıyor mu?
- Connection string doğru mu?
- Veritabanı adı doğru mu?
- Kullanıcı yetkili mi?
- `key.json`, user-secrets veya environment variable doğru mu?

### Ödeme Başarısız

Kontrol edilecekler:

- iyzico sandbox API key doğru mu?
- iyzico sandbox secret key doğru mu?
- Test kartı bilgileri doğru formatta mı?
- Son kullanma tarihi `MM` ve `YY` şeklinde mi?
- CVC 3 veya 4 haneli mi?

### Arama Sonuçları Gelmiyor

Kontrol edilecekler:

- Alış yeri autocomplete önerisi seçiliyor mu?
- Tarih seçici açılıyor mu?
- Geçmiş tarih seçilmeye çalışılıyor mu?
- Yolcu360 sonuç sayfasında gerçekten sonuç var mı?
- Filtreler sonucu sıfırlıyor olabilir mi?

### Session Çalışmıyor

Kontrol edilecekler:

- `sessions/` klasöründe kullanıcıya ait JSON var mı?
- Kullanıcının `SessionStatePath` alanı doğru mu?
- Yolcu360 oturumu site tarafında geçersiz hale gelmiş olabilir mi?

## Notlar

- `html/` klasörü, Yolcu360 sayfasından alınan inspect HTML parçalarını tutmak için kullanılır.
- `md/` klasörü, kod akışlarını ve teknik açıklamaları saklar.
- `sessions/` klasörü kullanıcı oturum verilerini içerir; gerçek kullanıcı verisi taşıdığı için repository'ye dahil edilmemelidir.
- `key.json` veya `appsettings.json` içinde gerçek connection string ve API key varsa repository'ye eklenmemelidir.

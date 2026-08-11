# Selector Notes

Bu dosya, sadeleştirme sırasında ortak kullanılması planlanan selector kararlarını tutar.

## Phone Input

Mevcut tekrar eden kullanım:

```js
document.querySelector('#phn-input') || document.querySelector('input[type="tel"]')
```

Önerilen tek selector:

```js
document.querySelector('#phn-input, input[type="tel"]')
```

C# tarafında önerilen sabit:

```csharp
private const string PhoneInputSelector = "#phn-input, input[type=\"tel\"]";
```

Sebep:

- `#phn-input` Yolcu360 telefon inputunun net ID'si.
- `input[type="tel"]` ID değişirse yedek selector olarak kalır.
- Tek `querySelector` çağrısı okunabilirliği artırır.
- Aynı selector birçok auth scriptinde tekrar edildiği için C# tarafında sabitlenmesi daha doğru olur.

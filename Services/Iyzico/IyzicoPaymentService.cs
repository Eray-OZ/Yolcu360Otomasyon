using System.Diagnostics;
using System.Globalization;
using Iyzipay;
using Iyzipay.Model;
using Iyzipay.Request;
using Yolcu360Otomasyon.Configuration;
using Yolcu360Otomasyon.Models;

namespace Yolcu360Otomasyon.Services;

public sealed class IyzicoPaymentService
{
    private readonly IyzicoSettings _settings;
    private readonly IyzicoCallbackService _callbackService;

    public IyzicoPaymentService(IyzicoSettings settings, IyzicoCallbackService callbackService)
    {
        _settings = settings;
        _callbackService = callbackService;
    }

    public async Task<IyzicoCheckoutSession> InitializeCheckoutAsync(AppUser user, IReadOnlyCollection<OdemeHazirlikItem> items)
    {
        if (items.Count == 0)
            throw new InvalidOperationException("Odeme icin secili kayit bulunamadi.");

        await _callbackService.StartAsync();

        var totalAmount = items.Sum(item => item.Tutar);
        var conversationId = Guid.NewGuid().ToString("N");
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

        var result = await CheckoutFormInitialize.Create(request, BuildOptions());
        if (!string.Equals(result.Status, "success", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(result.ErrorMessage ?? "iyzico checkout initialize başarısız.");

        if (string.IsNullOrWhiteSpace(result.Token) || string.IsNullOrWhiteSpace(result.PaymentPageUrl))
            throw new InvalidOperationException("iyzico ödeme sayfası oluşturulamadı.");

        return new IyzicoCheckoutSession
        {
            ConversationId = conversationId,
            Token = result.Token,
            PaymentPageUrl = result.PaymentPageUrl,
            TotalAmount = totalAmount
        };
    }

    public void OpenCheckoutPage(string paymentPageUrl)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = paymentPageUrl,
            UseShellExecute = true
        });
    }

    public Task<IyzicoCallbackPayload> WaitForCallbackAsync(string token, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        return _callbackService.WaitForCallbackAsync(token, timeout, cancellationToken);
    }

    public async Task<IyzicoPaymentResult> RetrievePaymentResultAsync(string conversationId, string token)
    {
        var request = new RetrieveCheckoutFormRequest
        {
            Locale = Locale.TR.ToString(),
            ConversationId = conversationId,
            Token = token
        };

        var result = await CheckoutForm.Retrieve(request, BuildOptions());

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
    }

    private Options BuildOptions()
    {
        return new Options
        {
            ApiKey = _settings.ApiKey,
            SecretKey = _settings.SecretKey,
            BaseUrl = _settings.BaseUrl
        };
    }

    private static Buyer BuildBuyer(AppUser user)
    {
        var emailPrefix = user.Email.Split('@', StringSplitOptions.RemoveEmptyEntries)[0];
        var normalizedName = new string(emailPrefix.Where(char.IsLetter).ToArray());
        if (string.IsNullOrWhiteSpace(normalizedName))
            normalizedName = "Yolcu";

        return new Buyer
        {
            Id = user.Id.ToString(CultureInfo.InvariantCulture),
            Name = normalizedName,
            Surname = "Kullanici",
            GsmNumber = NormalizePhoneNumber(user.PhoneNumber),
            Email = user.Email,
            IdentityNumber = "11111111111",
            LastLoginDate = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            RegistrationDate = user.CreatedAt == default
                ? DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
                : user.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            RegistrationAddress = "Istanbul",
            Ip = "127.0.0.1",
            City = "Istanbul",
            Country = "Turkey",
            ZipCode = "34000"
        };
    }

    private static Address BuildAddress(AppUser user)
    {
        return new Address
        {
            ContactName = user.Email,
            City = "Istanbul",
            Country = "Turkey",
            Description = "Yolcu360 otomasyon sandbox odeme adresi",
            ZipCode = "34000"
        };
    }

    private static BasketItem BuildBasketItem(OdemeHazirlikItem item)
    {
        return new BasketItem
        {
            Id = item.KoleksiyonId.ToString(CultureInfo.InvariantCulture),
            Name = item.KoleksiyonAdi,
            Category1 = "Arac Kiralama",
            ItemType = BasketItemType.VIRTUAL.ToString(),
            Price = FormatPrice(item.Tutar)
        };
    }

    private static string FormatPrice(decimal value)
    {
        return value.ToString("0.00", CultureInfo.InvariantCulture);
    }

    private static string NormalizePhoneNumber(string phoneNumber)
    {
        var digits = new string(phoneNumber.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("90", StringComparison.Ordinal))
            return $"+{digits}";

        if (digits.StartsWith("0", StringComparison.Ordinal))
            digits = digits[1..];

        return $"+90{digits}";
    }
}

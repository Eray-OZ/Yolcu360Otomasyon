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

    public IyzicoPaymentService(IyzicoSettings settings)
    {
        _settings = settings;
    }

    public async Task<IyzicoPaymentResult> CreateDirectPaymentAsync(
        AppUser user,
        IReadOnlyCollection<OdemeHazirlikItem> items,
        SandboxPaymentCardInput cardInput)
    {
        if (items.Count == 0)
            throw new InvalidOperationException("Odeme icin secili kayit bulunamadi.");

        ValidateSandboxCardInput(cardInput);

        var totalAmount = items.Sum(item => item.Tutar);
        var conversationId = Guid.NewGuid().ToString("N");
        var request = new CreatePaymentRequest
        {
            Locale = Locale.TR.ToString(),
            ConversationId = conversationId,
            Price = FormatPrice(totalAmount),
            PaidPrice = FormatPrice(totalAmount),
            Currency = Currency.TRY.ToString(),
            Installment = 1,
            BasketId = $"KOL-{user.Id}-{DateTime.UtcNow:yyyyMMddHHmmss}",
            PaymentGroup = PaymentGroup.PRODUCT.ToString(),
            PaymentCard = BuildPaymentCard(cardInput),
            Buyer = BuildBuyer(user),
            ShippingAddress = BuildAddress(user),
            BillingAddress = BuildAddress(user),
            BasketItems = items.Select(BuildBasketItem).ToList()
        };

        var result = await Payment.Create(request, BuildOptions());

        return new IyzicoPaymentResult
        {
            ConversationId = conversationId,
            Token = result.PaymentId ?? conversationId,
            ReferenceNo = result.PaymentId ?? conversationId,
            Status = result.Status ?? string.Empty,
            PaymentStatus = result.PaymentStatus ?? string.Empty,
            Provider = "iyzico-sandbox",
            CardAssociation = result.CardAssociation,
            LastFourDigits = result.LastFourDigits,
            CardHolderName = cardInput.CardHolderName,
            ErrorMessage = result.ErrorMessage
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
            Id = item.KoleksiyonId?.ToString(CultureInfo.InvariantCulture) ?? Guid.NewGuid().ToString("N"),
            Name = item.KoleksiyonAdi,
            Category1 = item.KoleksiyonAdi.StartsWith("[Uçak Bileti]") ? "Ucak Bileti" : "Arac Kiralama",
            ItemType = BasketItemType.VIRTUAL.ToString(),
            Price = FormatPrice(item.Tutar)
        };
    }

    private static PaymentCard BuildPaymentCard(SandboxPaymentCardInput cardInput)
    {
        return new PaymentCard
        {
            CardHolderName = cardInput.CardHolderName,
            CardNumber = NormalizeDigits(cardInput.CardNumber),
            ExpireMonth = NormalizeDigits(cardInput.ExpiryMonth),
            ExpireYear = NormalizeDigits(cardInput.ExpiryYear),
            Cvc = NormalizeDigits(cardInput.Cvc),
            RegisterCard = 0
        };
    }

    private static void ValidateSandboxCardInput(SandboxPaymentCardInput cardInput)
    {
        if (string.IsNullOrWhiteSpace(cardInput.CardHolderName))
            throw new InvalidOperationException("Kart sahibi adı boş.");

        if (NormalizeDigits(cardInput.CardNumber).Length < 15)
            throw new InvalidOperationException("Kart numarası geçersiz.");

        if (NormalizeDigits(cardInput.ExpiryMonth).Length != 2 || NormalizeDigits(cardInput.ExpiryYear).Length != 2)
            throw new InvalidOperationException("Son kullanma tarihi MM/YY formatında olmalı.");

        var cvcLength = NormalizeDigits(cardInput.Cvc).Length;
        if (cvcLength is < 3 or > 4)
            throw new InvalidOperationException("CVC geçersiz.");
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

    private static string NormalizeDigits(string value)
    {
        return new string((value ?? string.Empty).Where(char.IsDigit).ToArray());
    }
}

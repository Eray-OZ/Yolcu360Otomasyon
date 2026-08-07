namespace Yolcu360Otomasyon.Configuration;

public sealed class IyzicoSettings
{
    public string ApiKey { get; init; } = string.Empty;
    public string SecretKey { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = "https://sandbox-api.iyzipay.com";
}

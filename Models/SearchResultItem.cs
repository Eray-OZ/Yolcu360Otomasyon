using System.Text.Json.Serialization;

namespace Yolcu360Otomasyon.Models;

public sealed class SearchResultItem
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("subtitle")]
    public string Subtitle { get; set; } = string.Empty;

    [JsonPropertyName("price")]
    public string Price { get; set; } = string.Empty;

    [JsonPropertyName("dailyPrice")]
    public string DailyPrice { get; set; } = string.Empty;

    [JsonPropertyName("transmission")]
    public string Transmission { get; set; } = string.Empty;

    [JsonPropertyName("fuelType")]
    public string FuelType { get; set; } = string.Empty;

    [JsonPropertyName("supplier")]
    public string Supplier { get; set; } = string.Empty;

    [JsonPropertyName("pickupInfo")]
    public string PickupInfo { get; set; } = string.Empty;

    [JsonPropertyName("actionText")]
    public string ActionText { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}

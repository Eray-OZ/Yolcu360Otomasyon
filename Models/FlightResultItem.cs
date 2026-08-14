using System.Text.Json.Serialization;

namespace Yolcu360Otomasyon.Models;

public sealed class FlightResultItem
{
    [JsonPropertyName("airline")]
    public string Airline { get; set; } = string.Empty;

    [JsonPropertyName("route")]
    public string Route { get; set; } = string.Empty;

    [JsonPropertyName("departureTime")]
    public string DepartureTime { get; set; } = string.Empty;

    [JsonPropertyName("arrivalTime")]
    public string ArrivalTime { get; set; } = string.Empty;

    [JsonPropertyName("duration")]
    public string Duration { get; set; } = string.Empty;

    [JsonPropertyName("price")]
    public string Price { get; set; } = string.Empty;

    [JsonPropertyName("detail")]
    public string Detail { get; set; } = string.Empty;
}

namespace Yolcu360Otomasyon.Models;

public sealed class FlightSearchFilter
{
    public string FromLocation { get; set; } = string.Empty;
    public string FromPlaceCode { get; set; } = string.Empty;
    public string FromPlaceId { get; set; } = string.Empty;
    public string FromPlaceType { get; set; } = string.Empty;
    public string ToLocation { get; set; } = string.Empty;
    public string ToPlaceCode { get; set; } = string.Empty;
    public string ToPlaceId { get; set; } = string.Empty;
    public string ToPlaceType { get; set; } = string.Empty;
    public DateTime DepartureDate { get; set; }
    public DateTime? ReturnDate { get; set; }
    public bool IsRoundTrip { get; set; }
    public bool OnlyNonStop { get; set; }
}

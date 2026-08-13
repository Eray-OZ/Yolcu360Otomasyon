namespace Yolcu360Otomasyon.Models;

public sealed class FlightSearchFilter
{
    public string FromLocation { get; set; } = string.Empty;
    public string ToLocation { get; set; } = string.Empty;
    public DateTime DepartureDate { get; set; }
    public DateTime? ReturnDate { get; set; }
    public bool IsRoundTrip { get; set; }
    public bool OnlyNonStop { get; set; }
}

namespace Yolcu360Otomasyon.Models;

public sealed class SearchFilter
{
    public string PickupLocation { get; set; } = string.Empty;
    public DateTime PickupDate { get; set; }
    public DateTime ReturnDate { get; set; }
    public string PickupTime { get; set; } = "10:00";
    public string ReturnTime { get; set; } = "10:00";
    public string TransmissionType { get; set; } = string.Empty;
    public string FuelType { get; set; } = string.Empty;
}

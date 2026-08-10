namespace Yolcu360Otomasyon.Services;

public sealed partial class BAService
{
    private static string[] GetTransmissionFilterTargets(string? transmissionType)
    {
        var transmissionNorm = transmissionType?.Trim().ToLowerInvariant();
        return transmissionNorm switch
        {
            "otomatik" or "automatic" => ["otomatik"],
            "manuel" or "manual" => ["manuel"],
            _ => []
        };
    }

    private static string[] GetFuelFilterTargets(string? fuelType)
    {
        var fuelNorm = fuelType?.Trim().ToLowerInvariant();
        return fuelNorm switch
        {
            "dizel" or "diesel" => ["dizel", "benzin/dizel"],
            "benzin" or "gasoline" => ["benzin", "benzin/dizel"],
            _ => []
        };
    }
}

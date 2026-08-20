// Extra - Car Comparison START
namespace Yolcu360Otomasyon.Models;

public sealed class CarComparisonItem
{
    public SearchResultItem Vehicle { get; set; } = new();
    public decimal DailyPriceNumeric { get; set; }
    public decimal TotalPriceNumeric { get; set; }
    public bool IsCheapest { get; set; }
    public decimal DifferenceFromCheapest { get; set; }
    public double PercentageDifference { get; set; }
    public string PriceBadgeText { get; set; } = string.Empty;
    public string PriceBadgeColor { get; set; } = "#64748B"; // #16A34A (green) or #DC2626 (red) or #64748B
    public List<string> AdvantageBadges { get; set; } = new();
    public string SourceCollectionName { get; set; } = string.Empty;
}
// Extra - Car Comparison END

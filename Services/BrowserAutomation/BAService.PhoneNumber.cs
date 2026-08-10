namespace Yolcu360Otomasyon.Services;

public sealed partial class BAService
{
    private static string NormalizePhoneNumber(string raw)
    {
        var digits = new string(raw.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("90") && digits.Length == 12)
            digits = digits[2..];
        if (digits.StartsWith("0") && digits.Length == 11)
            digits = digits[1..];
        return digits;
    }

    private static List<string> SplitPhoneNumber(string number)
    {
        if (number.Length == 10)
        {
            return new List<string>
            {
                number.Substring(0, 3),
                number.Substring(3, 3),
                number.Substring(6, 2),
                number.Substring(8, 2)
            };
        }

        return new List<string> { number };
    }
}

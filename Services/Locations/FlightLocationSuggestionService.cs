using System.Text.Json;
using Yolcu360Otomasyon.Models;

namespace Yolcu360Otomasyon.Services;

// Extra - Flight Location Suggestion START
public sealed class FlightLocationSuggestionService
{
    private static readonly HttpClient HttpClient = new()
    {
        BaseAddress = new Uri("https://maps.cms.yolcu360.com")
    };

    public async Task<List<LocationSuggestionItem>> GetSuggestionsAsync(string input, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input) || input.Trim().Length < 2)
            return new List<LocationSuggestionItem>();

        var query = Uri.EscapeDataString(input.Trim());
        var url = $"/api/airports/search?locale=tr&input={query}";

        using var response = await HttpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (document.RootElement.ValueKind != JsonValueKind.Array)
            return new List<LocationSuggestionItem>();

        return document.RootElement
            .EnumerateArray()
            .Select(ParseAirport)
            .Where(item => !string.IsNullOrWhiteSpace(item.MainText))
            .Take(8)
            .ToList();
    }

    public async Task<LocationSuggestionItem?> ResolveBestSuggestionAsync(string input, CancellationToken cancellationToken = default)
    {
        var suggestions = await GetSuggestionsAsync(input, cancellationToken);
        if (suggestions.Count > 0)
            return PickBestSuggestion(input, suggestions);

        var airportCode = ExtractAirportCode(input);
        if (!string.IsNullOrWhiteSpace(airportCode))
        {
            suggestions = await GetSuggestionsAsync(airportCode, cancellationToken);
            if (suggestions.Count > 0)
                return PickBestSuggestion(input, suggestions);
        }

        var simplified = SimplifyAirportSearchText(input);
        if (!string.Equals(simplified, input, StringComparison.OrdinalIgnoreCase))
        {
            suggestions = await GetSuggestionsAsync(simplified, cancellationToken);
            if (suggestions.Count > 0)
                return PickBestSuggestion(input, suggestions);
        }

        return null;
    }

    private static LocationSuggestionItem ParseAirport(JsonElement airport)
    {
        var placeName = ReadString(airport, "placeName");
        var cityName = ReadString(airport, "cityName");
        var countryName = ReadString(airport, "countryName");
        var placeCode = ReadString(airport, "placeCode");
        var placeId = ReadString(airport, "placeId");
        var countryCode = ReadString(airport, "countryCode");
        var isCity = airport.TryGetProperty("isCity", out var isCityValue) &&
            isCityValue.ValueKind == JsonValueKind.True;

        var secondaryText = string.IsNullOrWhiteSpace(countryName)
            ? cityName
            : countryName;

        return new LocationSuggestionItem
        {
            MainText = placeName,
            SecondaryText = secondaryText,
            Description = string.IsNullOrWhiteSpace(secondaryText) ? placeName : $"{placeName}, {secondaryText}",
            PlaceId = placeId,
            PlaceCode = placeCode,
            Type = isCity ? "city" : "airport",
            City = cityName,
            CountryCode = countryCode,
            CountryName = countryName
        };
    }

    private static string ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
    }

    private static LocationSuggestionItem PickBestSuggestion(string input, List<LocationSuggestionItem> suggestions)
    {
        var normalizedInput = Normalize(input);
        var airportCode = ExtractAirportCode(input);

        return suggestions
            .OrderBy(item =>
            {
                if (!string.IsNullOrWhiteSpace(airportCode) &&
                    string.Equals(item.PlaceCode, airportCode, StringComparison.OrdinalIgnoreCase))
                {
                    return 0;
                }

                var mainText = Normalize(item.MainText);
                var description = Normalize(item.Description);

                if (mainText == normalizedInput) return 1;
                if (description == normalizedInput) return 2;
                if (normalizedInput.Contains(mainText) || mainText.Contains(normalizedInput)) return 3;
                if (description.Contains(normalizedInput) || normalizedInput.Contains(description)) return 4;
                return 5;
            })
            .First();
    }

    private static string ExtractAirportCode(string input)
    {
        var text = input?.Trim() ?? string.Empty;
        var openParenIndex = text.LastIndexOf('(');
        var closeParenIndex = text.LastIndexOf(')');

        if (openParenIndex >= 0 && closeParenIndex > openParenIndex)
        {
            var code = text[(openParenIndex + 1)..closeParenIndex].Trim();
            if (code.Length == 3 && code.All(char.IsLetter))
                return code.ToUpperInvariant();
        }

        return string.Empty;
    }

    private static string SimplifyAirportSearchText(string input)
    {
        var text = input?.Trim() ?? string.Empty;
        var commaIndex = text.IndexOf(',');
        if (commaIndex > 0)
            text = text[..commaIndex].Trim();

        return text
            .Replace("Uluslararası Havalimanı", "Havalimanı", StringComparison.OrdinalIgnoreCase)
            .Replace("International Airport", "Airport", StringComparison.OrdinalIgnoreCase)
            .Replace("Airport", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("Havalimanı", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();
    }

    private static string Normalize(string value)
    {
        return (value ?? string.Empty)
            .ToLocaleLowerTr()
            .Replace("ı", "i", StringComparison.Ordinal)
            .Replace("ğ", "g", StringComparison.Ordinal)
            .Replace("ü", "u", StringComparison.Ordinal)
            .Replace("ş", "s", StringComparison.Ordinal)
            .Replace("ö", "o", StringComparison.Ordinal)
            .Replace("ç", "c", StringComparison.Ordinal)
            .Trim();
    }
}

file static class FlightLocationSuggestionStringExtensions
{
    public static string ToLocaleLowerTr(this string value)
    {
        return value.ToLower(new System.Globalization.CultureInfo("tr-TR"));
    }
}
// Extra - Flight Location Suggestion END

using System.Text.Json;
using Yolcu360Otomasyon.Models;

namespace Yolcu360Otomasyon.Services;

// Extra - Location Suggestion START
public sealed class LocationSuggestionService
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
        var url = $"/api/maps/autocomplete?language=tr&input={query}&locationbias=circle:100000@40.731647,31.589813";

        using var response = await HttpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty("predictions", out var predictions) ||
            predictions.ValueKind != JsonValueKind.Array)
        {
            return new List<LocationSuggestionItem>();
        }

        return predictions
            .EnumerateArray()
            .Select(ParsePrediction)
            .Where(item => !string.IsNullOrWhiteSpace(item.MainText))
            .Take(8)
            .ToList();
    }

    private static LocationSuggestionItem ParsePrediction(JsonElement prediction)
    {
        var description = ReadString(prediction, "description");
        var placeId = ReadString(prediction, "place_id");
        var mainText = string.Empty;
        var secondaryText = string.Empty;

        if (prediction.TryGetProperty("structured_formatting", out var formatting))
        {
            mainText = ReadString(formatting, "main_text");
            secondaryText = ReadString(formatting, "secondary_text");
        }

        return new LocationSuggestionItem
        {
            MainText = string.IsNullOrWhiteSpace(mainText) ? description : mainText,
            SecondaryText = secondaryText,
            Description = description,
            PlaceId = placeId
        };
    }

    private static string ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
    }
}
// Extra - Location Suggestion END

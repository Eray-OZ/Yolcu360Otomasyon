using System.Text.Json;
using System.Text.RegularExpressions;

namespace Yolcu360Otomasyon.Configuration;

public static class AppSettings
{
    private const string UserSecretsId = "Yolcu360Otomasyon-Development";
    private const string ConnectionStringKey = "ConnectionStrings:Yolcu360Database";
    private const string EnvironmentKey = "YOLCU360_CONNECTION_STRING";
    private const string IyzicoApiKeyEnvironmentKey = "IYZ_API_KEY";
    private const string IyzicoSecretKeyEnvironmentKey = "IYZ_SECURITY_KEY";

    public static string GetConnectionString()
    {
        // Önce user-secrets, sonra environment variable, sonra local key.json okunur.
        var connectionString = ReadUserSecret(ConnectionStringKey)
            ?? Environment.GetEnvironmentVariable(EnvironmentKey)
            ?? ReadConnectionStringFromLocalFiles();

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "MySQL connection string bulunamadi. User-secrets veya YOLCU360_CONNECTION_STRING ekleyin.");
        }

        return connectionString;
    }

    public static IyzicoSettings GetIyzicoSettings()
    {
        var apiKey = Environment.GetEnvironmentVariable(IyzicoApiKeyEnvironmentKey)
            ?? ReadValueFromLocalFiles("IYZ_API_KEY");
        var secretKey = Environment.GetEnvironmentVariable(IyzicoSecretKeyEnvironmentKey)
            ?? ReadValueFromLocalFiles("IYZ_SECURITY_KEY");

        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(secretKey))
        {
            throw new InvalidOperationException(
                "iyzico sandbox bilgileri bulunamadi. appsettings.json veya ortam degiskenlerini kontrol edin.");
        }

        return new IyzicoSettings
        {
            ApiKey = apiKey,
            SecretKey = secretKey,
            BaseUrl = "https://sandbox-api.iyzipay.com"
        };
    }

    private static string? ReadUserSecret(string key)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var secretsPath = Path.Combine(home, ".microsoft", "usersecrets", UserSecretsId, "secrets.json");

        if (!File.Exists(secretsPath))
            return null;

        using var stream = File.OpenRead(secretsPath);
        using var document = JsonDocument.Parse(stream);

        // dotnet user-secrets key'i genelde düz string key olarak saklar.
        if (document.RootElement.TryGetProperty(key, out var flatValue))
            return flatValue.GetString();

        // Manuel yazılmış nested JSON kullanımını da destekler.
        if (document.RootElement.TryGetProperty("ConnectionStrings", out var connectionStrings)
            && connectionStrings.TryGetProperty("Yolcu360Database", out var nestedValue))
        {
            return nestedValue.GetString();
        }

        return null;
    }

    private static string? ReadConnectionStringFromLocalFiles()
    {
        foreach (var candidatePath in EnumerateConfigFiles())
        {
            if (!File.Exists(candidatePath))
                continue;

            var raw = File.ReadAllText(candidatePath);

            try
            {
                using var document = JsonDocument.Parse(raw);
                if (document.RootElement.TryGetProperty("ConnectionStrings", out var connectionStrings))
                {
                    if (connectionStrings.TryGetProperty("Yolcu360Database", out var yolcu360Value))
                        return yolcu360Value.GetString();

                    if (connectionStrings.TryGetProperty("DefaultConnection", out var defaultValue))
                        return defaultValue.GetString();
                }
            }
            catch (JsonException)
            {
                var match = Regex.Match(
                    raw,
                    "\"DefaultConnection\"\\s*:\\s*\"(?<value>[^\"]+)\"",
                    RegexOptions.IgnoreCase);

                if (match.Success)
                    return match.Groups["value"].Value;
            }
        }

        return null;
    }

    private static string? ReadValueFromLocalFiles(string key)
    {
        foreach (var candidatePath in EnumerateConfigFiles())
        {
            if (!File.Exists(candidatePath))
                continue;

            using var stream = File.OpenRead(candidatePath);
            using var document = JsonDocument.Parse(stream);

            if (document.RootElement.TryGetProperty(key, out var value))
                return value.GetString();
        }

        return null;
    }

    private static IEnumerable<string> EnumerateConfigFiles()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            yield return Path.Combine(current.FullName, "key.json");
            yield return Path.Combine(current.FullName, "appsettings.json");
            yield return Path.Combine(current.FullName, "Others", "key.json");
            current = current.Parent;
        }
    }
}

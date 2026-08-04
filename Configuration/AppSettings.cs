using System.Text.Json;

namespace Yolcu360Otomasyon.Configuration;

public static class AppSettings
{
    private const string UserSecretsId = "Yolcu360Otomasyon-Development";
    private const string ConnectionStringKey = "ConnectionStrings:Yolcu360Database";
    private const string EnvironmentKey = "YOLCU360_CONNECTION_STRING";

    public static string GetConnectionString()
    {
        // Önce user-secrets, sonra environment variable okunur.
        var connectionString = ReadUserSecret(ConnectionStringKey)
            ?? Environment.GetEnvironmentVariable(EnvironmentKey);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "MySQL connection string bulunamadi. User-secrets veya YOLCU360_CONNECTION_STRING ekleyin.");
        }

        return connectionString;
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
}

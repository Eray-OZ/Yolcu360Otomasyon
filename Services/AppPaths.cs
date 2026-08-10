namespace Yolcu360Otomasyon.Services;

public static class AppPaths
{
    public static string BuildSessionStatePath(string email)
    {
        var safeFileName = string.Concat(email.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_'));
        var sessionsDirectory = Path.Combine(ResolveAppDataDirectory(), "sessions");
        return Path.Combine(sessionsDirectory, $"{safeFileName}.json");
    }

    public static string ResolveAppDataDirectory()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Yolcu360Otomasyon.csproj")))
                return current.FullName;

            current = current.Parent;
        }

        return AppContext.BaseDirectory;
    }
}

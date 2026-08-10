using Yolcu360Otomasyon.Models;

namespace Yolcu360Otomasyon.Services.Auth;

public sealed record AuthWorkflowResult(
    bool Success,
    AppUser? User,
    string? ErrorMessage = null,
    bool UsedSavedSession = false)
{
    public static AuthWorkflowResult Failed(string errorMessage)
    {
        return new AuthWorkflowResult(false, null, errorMessage);
    }
}

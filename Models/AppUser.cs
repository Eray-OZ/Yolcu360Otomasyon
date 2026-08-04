namespace Yolcu360Otomasyon.Models;

public sealed class AppUser
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

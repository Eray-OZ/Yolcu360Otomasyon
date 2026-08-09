using Microsoft.EntityFrameworkCore;
using Yolcu360Otomasyon.Data;
using Yolcu360Otomasyon.Models;

namespace Yolcu360Otomasyon.Services;

public sealed partial class DatabaseService
{
    public async Task<AppUser?> GetUserByCredentialsAsync(string email, string password)
    {
        await EnsureSchemaAsync();
        await using var context = new AppDbContext(_options);

        return await context.Kullanicilar
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.Email == email && user.Password == password);
    }

    public async Task<AppUser?> GetUserByEmailAsync(string email)
    {
        await EnsureSchemaAsync();
        await using var context = new AppDbContext(_options);

        return await context.Kullanicilar
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.Email == email);
    }

    public async Task SaveOrUpdateUserAsync(string email, string password, string phoneNumber, string sessionStatePath)
    {
        await EnsureSchemaAsync();
        await using var context = new AppDbContext(_options);
        var existingUser = await context.Kullanicilar.FirstOrDefaultAsync(user => user.Email == email);
        var now = DateTime.UtcNow;

        if (existingUser is null)
        {
            context.Kullanicilar.Add(new AppUser
            {
                Email = email,
                Password = password,
                PhoneNumber = phoneNumber,
                SessionStatePath = sessionStatePath,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
        else
        {
            existingUser.Password = password;
            existingUser.PhoneNumber = phoneNumber;
            existingUser.SessionStatePath = sessionStatePath;
            existingUser.UpdatedAt = now;
        }

        await context.SaveChangesAsync();
    }

    public async Task<bool> UserExistsAsync(string email)
    {
        await EnsureSchemaAsync();
        await using var context = new AppDbContext(_options);
        return await context.Kullanicilar.AsNoTracking().AnyAsync(user => user.Email == email);
    }
}

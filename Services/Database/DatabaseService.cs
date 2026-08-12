using Microsoft.EntityFrameworkCore;
using Yolcu360Otomasyon.Data;

namespace Yolcu360Otomasyon.Services;

public sealed partial class DatabaseService
{
    private readonly DbContextOptions<AppDbContext> _options;
    private readonly SemaphoreSlim _schemaLock = new(1, 1);
    private bool _schemaReady;

    public DatabaseService(string connectionString)
    {
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))
            .Options;
    }

    public async Task EnsureDatabaseAsync()
    {
        if (_schemaReady)
            return;

        await _schemaLock.WaitAsync();
        try
        {
            if (_schemaReady)
                return;

            await using var context = new AppDbContext(_options);
            await context.Database.EnsureCreatedAsync();

            _schemaReady = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DatabaseService] Schema initialization note: {ex.Message}");
        }
        finally
        {
            _schemaLock.Release();
        }
    }

    private Task EnsureSchemaAsync() => EnsureDatabaseAsync();
}

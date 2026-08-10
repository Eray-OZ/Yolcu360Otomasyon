using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using Yolcu360Otomasyon.Data;

namespace Yolcu360Otomasyon.Services;

public sealed partial class DatabaseService
{
    private readonly DbContextOptions<AppDbContext> _options;
    private readonly string _connectionString;
    private readonly SemaphoreSlim _schemaLock = new(1, 1);
    private bool _schemaReady;

    public DatabaseService(string connectionString)
    {
        _connectionString = connectionString;
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

            await EnsureDatabaseExistsAsync();

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

    private async Task EnsureDatabaseExistsAsync()
    {
        try
        {
            var builder = new MySqlConnectionStringBuilder(_connectionString);
            var databaseName = builder.Database;

            if (!string.IsNullOrWhiteSpace(databaseName))
            {
                builder.Database = string.Empty;
                await using var connection = new MySqlConnection(builder.ConnectionString);
                await connection.OpenAsync();

                await using var command = connection.CreateCommand();
                command.CommandText = $"CREATE DATABASE IF NOT EXISTS `{databaseName}` DEFAULT CHARACTER SET utf8mb4;";
                await command.ExecuteNonQueryAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DatabaseService] Database creation note: {ex.Message}");
        }
    }

    private Task EnsureSchemaAsync() => EnsureDatabaseAsync();
}

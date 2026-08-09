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
        await EnsureSchemaAsync();
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

    private async Task EnsureSchemaAsync()
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
            await context.Database.ExecuteSqlRawAsync("DROP TABLE IF EXISTS users;");
            await context.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS kullanicilar (
                    Id INT NOT NULL AUTO_INCREMENT,
                    Email VARCHAR(255) NOT NULL,
                    Password VARCHAR(255) NOT NULL,
                    PhoneNumber VARCHAR(32) NOT NULL,
                    SessionStatePath VARCHAR(512) NOT NULL,
                    CreatedAt DATETIME(6) NOT NULL,
                    UpdatedAt DATETIME(6) NOT NULL,
                    CONSTRAINT PK_kullanicilar PRIMARY KEY (Id),
                    CONSTRAINT UX_kullanicilar_Email UNIQUE (Email)
                );
                """);

            await context.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS koleksiyonlar (
                    Id INT NOT NULL AUTO_INCREMENT,
                    KullaniciId INT NOT NULL,
                    OzelAd VARCHAR(255) NOT NULL,
                    AlisYeri VARCHAR(255) NOT NULL,
                    AlisTarihi DATETIME(6) NOT NULL,
                    DonusTarihi DATETIME(6) NOT NULL,
                    AlisSaati VARCHAR(16) NOT NULL,
                    DonusSaati VARCHAR(16) NOT NULL,
                    SecilenVitesFiltresi VARCHAR(64) NULL,
                    SecilenYakitFiltresi VARCHAR(64) NULL,
                    OlusturmaTarihi DATETIME(6) NOT NULL,
                    CONSTRAINT PK_koleksiyonlar PRIMARY KEY (Id),
                    CONSTRAINT FK_koleksiyonlar_kullanicilar_KullaniciId
                        FOREIGN KEY (KullaniciId) REFERENCES kullanicilar (Id)
                        ON DELETE CASCADE
                );
                """);

            await context.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS araclar (
                    Id INT NOT NULL AUTO_INCREMENT,
                    KoleksiyonId INT NOT NULL,
                    Baslik VARCHAR(255) NOT NULL,
                    AltBaslik VARCHAR(255) NULL,
                    Fiyat VARCHAR(64) NOT NULL,
                    GunlukFiyat VARCHAR(64) NULL,
                    Vites VARCHAR(64) NULL,
                    Yakit VARCHAR(64) NULL,
                    Sirket VARCHAR(128) NULL,
                    TeslimBilgisi VARCHAR(255) NULL,
                    IslemMetni VARCHAR(128) NULL,
                    Baglanti VARCHAR(1024) NULL,
                    CONSTRAINT PK_araclar PRIMARY KEY (Id),
                    CONSTRAINT FK_araclar_koleksiyonlar_KoleksiyonId
                        FOREIGN KEY (KoleksiyonId) REFERENCES koleksiyonlar (Id)
                        ON DELETE CASCADE
                );
                """);

            await context.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS odemeler (
                    Id INT NOT NULL AUTO_INCREMENT,
                    KullaniciId INT NOT NULL,
                    KoleksiyonId INT NOT NULL,
                    ReferansNo VARCHAR(64) NOT NULL,
                    KoleksiyonAdi VARCHAR(255) NOT NULL,
                    Tutar DECIMAL(18,2) NOT NULL,
                    ParaBirimi VARCHAR(8) NOT NULL,
                    Durum VARCHAR(32) NOT NULL,
                    Saglayici VARCHAR(64) NOT NULL,
                    KartSahibi VARCHAR(128) NULL,
                    KartSon4 VARCHAR(4) NULL,
                    OdemeTarihi DATETIME(6) NOT NULL,
                    CONSTRAINT PK_odemeler PRIMARY KEY (Id),
                    CONSTRAINT FK_odemeler_kullanicilar_KullaniciId
                        FOREIGN KEY (KullaniciId) REFERENCES kullanicilar (Id)
                        ON DELETE CASCADE,
                    CONSTRAINT FK_odemeler_koleksiyonlar_KoleksiyonId
                        FOREIGN KEY (KoleksiyonId) REFERENCES koleksiyonlar (Id)
                        ON DELETE CASCADE
                );
                """);

            await EnsureColumnAsync(context, "koleksiyonlar", "AlisYeri", "VARCHAR(255) NOT NULL DEFAULT ''");
            await EnsureColumnAsync(context, "koleksiyonlar", "AlisTarihi", "DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6)");
            await EnsureColumnAsync(context, "koleksiyonlar", "DonusTarihi", "DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6)");
            await EnsureColumnAsync(context, "koleksiyonlar", "AlisSaati", "VARCHAR(16) NOT NULL DEFAULT ''");
            await EnsureColumnAsync(context, "koleksiyonlar", "DonusSaati", "VARCHAR(16) NOT NULL DEFAULT ''");
            await EnsureColumnAsync(context, "koleksiyonlar", "SecilenVitesFiltresi", "VARCHAR(64) NULL");
            await EnsureColumnAsync(context, "koleksiyonlar", "SecilenYakitFiltresi", "VARCHAR(64) NULL");
            await context.Database.ExecuteSqlRawAsync("""
                UPDATE koleksiyonlar
                SET SecilenVitesFiltresi = ''
                WHERE SecilenVitesFiltresi IS NULL;
                """);
            await context.Database.ExecuteSqlRawAsync("""
                UPDATE koleksiyonlar
                SET SecilenYakitFiltresi = ''
                WHERE SecilenYakitFiltresi IS NULL;
                """);
            await EnsureColumnAsync(context, "odemeler", "KartSahibi", "VARCHAR(128) NULL");
            await EnsureColumnAsync(context, "odemeler", "KartSon4", "VARCHAR(4) NULL");

            await EnsureIndexAsync(context, "koleksiyonlar", "IX_koleksiyonlar_KullaniciId", "KullaniciId");
            await EnsureIndexAsync(context, "araclar", "IX_araclar_KoleksiyonId", "KoleksiyonId");
            await EnsureIndexAsync(context, "odemeler", "IX_odemeler_KullaniciId", "KullaniciId");
            await EnsureIndexAsync(context, "odemeler", "IX_odemeler_KoleksiyonId", "KoleksiyonId");

            _schemaReady = true;
        }
        finally
        {
            _schemaLock.Release();
        }
    }

    private static async Task EnsureIndexAsync(AppDbContext context, string tableName, string indexName, string columnName)
    {
        await using var connection = new MySqlConnection(context.Database.GetConnectionString());
        await connection.OpenAsync();

        await using var existsCommand = connection.CreateCommand();
        existsCommand.CommandText = """
            SELECT COUNT(*)
            FROM information_schema.statistics
            WHERE table_schema = DATABASE()
              AND table_name = @tableName
              AND index_name = @indexName;
            """;
        existsCommand.Parameters.AddWithValue("@tableName", tableName);
        existsCommand.Parameters.AddWithValue("@indexName", indexName);

        var exists = Convert.ToInt32(await existsCommand.ExecuteScalarAsync()) > 0;
        if (exists)
            return;

        await using var createCommand = connection.CreateCommand();
        createCommand.CommandText = $"CREATE INDEX {indexName} ON {tableName} ({columnName});";
        await createCommand.ExecuteNonQueryAsync();
    }

    private static async Task EnsureColumnAsync(AppDbContext context, string tableName, string columnName, string definitionSql)
    {
        await using var connection = new MySqlConnection(context.Database.GetConnectionString());
        await connection.OpenAsync();

        await using var existsCommand = connection.CreateCommand();
        existsCommand.CommandText = """
            SELECT COUNT(*)
            FROM information_schema.columns
            WHERE table_schema = DATABASE()
              AND table_name = @tableName
              AND column_name = @columnName;
            """;
        existsCommand.Parameters.AddWithValue("@tableName", tableName);
        existsCommand.Parameters.AddWithValue("@columnName", columnName);

        var exists = Convert.ToInt32(await existsCommand.ExecuteScalarAsync()) > 0;
        if (exists)
            return;

        await using var alterCommand = connection.CreateCommand();
        alterCommand.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {definitionSql};";
        await alterCommand.ExecuteNonQueryAsync();
    }
}

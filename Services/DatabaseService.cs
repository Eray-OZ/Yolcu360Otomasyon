using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using Yolcu360Otomasyon.Data;
using Yolcu360Otomasyon.Models;

namespace Yolcu360Otomasyon.Services;

public sealed class DatabaseService
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
        await EnsureSchemaAsync();
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

            await EnsureIndexAsync(context, "koleksiyonlar", "IX_koleksiyonlar_KullaniciId", "KullaniciId");
            await EnsureIndexAsync(context, "araclar", "IX_araclar_KoleksiyonId", "KoleksiyonId");

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

    public async Task<int> SaveCollectionAsync(int kullaniciId, string ozelAd, SearchFilter filter, IReadOnlyCollection<SearchResultItem> items)
    {
        await EnsureSchemaAsync();
        await using var context = new AppDbContext(_options);

        var kullaniciVarMi = await context.Kullanicilar
            .AsNoTracking()
            .AnyAsync(item => item.Id == kullaniciId);

        if (!kullaniciVarMi)
            throw new InvalidOperationException("Aktif kullanıcı kaydı veritabanında bulunamadı.");

        var koleksiyon = new Koleksiyon
        {
            KullaniciId = kullaniciId,
            OzelAd = ozelAd,
            AlisYeri = filter.PickupLocation,
            AlisTarihi = filter.PickupDate,
            DonusTarihi = filter.ReturnDate,
            AlisSaati = filter.PickupTime,
            DonusSaati = filter.ReturnTime,
            SecilenVitesFiltresi = filter.TransmissionType,
            SecilenYakitFiltresi = filter.FuelType,
            OlusturmaTarihi = DateTime.UtcNow,
            Araclar = items.Select(item => new Arac
            {
                Baslik = item.Title,
                AltBaslik = item.Subtitle,
                Fiyat = item.Price,
                GunlukFiyat = item.DailyPrice,
                Vites = item.Transmission,
                Yakit = item.FuelType,
                Sirket = item.Supplier,
                TeslimBilgisi = item.PickupInfo,
                IslemMetni = item.ActionText,
                Baglanti = item.Url
            }).ToList()
        };

        context.Koleksiyonlar.Add(koleksiyon);
        await context.SaveChangesAsync();
        return koleksiyon.Id;
    }

    public async Task<List<KoleksiyonListItem>> GetCollectionsAsync(int kullaniciId)
    {
        await EnsureSchemaAsync();
        await using var context = new AppDbContext(_options);

        return await context.Koleksiyonlar
            .AsNoTracking()
            .Where(item => item.KullaniciId == kullaniciId)
            .OrderByDescending(item => item.OlusturmaTarihi)
            .Select(item => new KoleksiyonListItem
            {
                Id = item.Id,
                OzelAd = item.OzelAd,
                AlisYeri = item.AlisYeri,
                AlisTarihi = item.AlisTarihi,
                DonusTarihi = item.DonusTarihi,
                AlisSaati = item.AlisSaati,
                DonusSaati = item.DonusSaati,
                SecilenVitesFiltresi = item.SecilenVitesFiltresi ?? string.Empty,
                SecilenYakitFiltresi = item.SecilenYakitFiltresi ?? string.Empty,
                AracSayisi = item.Araclar.Count,
                OlusturmaTarihi = item.OlusturmaTarihi
            })
            .ToListAsync();
    }

    public async Task<List<SearchResultItem>> GetCollectionVehiclesAsync(int koleksiyonId)
    {
        await EnsureSchemaAsync();
        await using var context = new AppDbContext(_options);

        return await context.Araclar
            .AsNoTracking()
            .Where(item => item.KoleksiyonId == koleksiyonId)
            .OrderBy(item => item.Id)
            .Select(item => new SearchResultItem
            {
                Title = item.Baslik,
                Subtitle = item.AltBaslik,
                Price = item.Fiyat,
                DailyPrice = item.GunlukFiyat,
                Transmission = item.Vites,
                FuelType = item.Yakit,
                Supplier = item.Sirket,
                PickupInfo = item.TeslimBilgisi,
                ActionText = item.IslemMetni,
                Url = item.Baglanti
            })
            .ToListAsync();
    }

    public async Task DeleteCollectionAsync(int koleksiyonId, int kullaniciId)
    {
        await EnsureSchemaAsync();
        await using var context = new AppDbContext(_options);
        var koleksiyon = await context.Koleksiyonlar
            .FirstOrDefaultAsync(item => item.Id == koleksiyonId && item.KullaniciId == kullaniciId);

        if (koleksiyon is null)
            return;

        context.Koleksiyonlar.Remove(koleksiyon);
        await context.SaveChangesAsync();
    }
}

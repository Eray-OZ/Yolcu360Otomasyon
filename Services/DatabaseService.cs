using MySqlConnector;
using Yolcu360Otomasyon.Models;

namespace Yolcu360Otomasyon.Services;

public sealed class DatabaseService
{
    private readonly string _connectionString;

    public DatabaseService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<AppUser?> GetDefaultUserAsync()
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        // Giriş için ilk kayıtlı kullanıcıyı alır.
        const string sql = """
            SELECT id, email, password
            FROM users
            ORDER BY id
            LIMIT 1;
            """;

        await using var command = new MySqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
            return null;

        return new AppUser
        {
            Id = reader.GetInt32("id"),
            Email = reader.GetString("email"),
            Password = reader.GetString("password")
        };
    }

    public async Task SaveLoginUserAsync(string email, string password)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        // Login bilgileri için tabloyu ilk kullanımda hazırlar.
        const string createTableSql = """
            CREATE TABLE IF NOT EXISTS users (
                id INT AUTO_INCREMENT PRIMARY KEY,
                email VARCHAR(255) NOT NULL,
                password VARCHAR(255) NOT NULL,
                created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
            );
            """;

        await using (var createCommand = new MySqlCommand(createTableSql, connection))
        {
            await createCommand.ExecuteNonQueryAsync();
        }

        // Tek aktif login kaydı tutmak için önce eski kayıtlar temizlenir.
        const string clearSql = "DELETE FROM users;";

        await using (var clearCommand = new MySqlCommand(clearSql, connection))
        {
            await clearCommand.ExecuteNonQueryAsync();
        }

        const string insertSql = """
            INSERT INTO users (email, password)
            VALUES (@email, @password);
            """;

        await using var insertCommand = new MySqlCommand(insertSql, connection);
        insertCommand.Parameters.AddWithValue("@email", email);
        insertCommand.Parameters.AddWithValue("@password", password);

        await insertCommand.ExecuteNonQueryAsync();
    }
}

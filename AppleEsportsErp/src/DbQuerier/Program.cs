using System;
using System.Threading.Tasks;
using Npgsql;

class Program
{
    static async Task Main()
    {
        var connStr = "Host=localhost;Port=5432;Database=gamecafe_erp;Username=gamecafe_admin;Password=GameCafe_Secure_2026!";
        await using var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync();

        Console.WriteLine("--- Users ---");
        await using (var cmd = new NpgsqlCommand("SELECT email, \"PasswordHash\" FROM users", conn))
        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                Console.WriteLine($"{reader.GetString(0)}: {reader.GetString(1)}");
            }
        }

        Console.WriteLine("--- Operators ---");
        await using (var cmd = new NpgsqlCommand("SELECT \"Username\", \"PasswordHash\" FROM operators", conn))
        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                Console.WriteLine($"{reader.GetString(0)}: {reader.GetString(1)}");
            }
        }

        Console.WriteLine("--- Members ---");
        await using (var cmd = new NpgsqlCommand("SELECT \"PhoneNumber\", \"Pin\" FROM members LIMIT 5", conn))
        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                Console.WriteLine($"{reader.GetString(0)}: {reader.GetString(1)}");
            }
        }
    }
}

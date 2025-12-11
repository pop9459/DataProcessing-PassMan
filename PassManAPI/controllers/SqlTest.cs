using MySqlConnector;
using System;
using System.Threading.Tasks;

namespace PassManAPI.Controllers
{
    public static class SqlTest
    {
        public static async Task RunAsync(string connectionString)
        {
            try
            {
                await using var connection = new MySqlConnection(connectionString);

                await connection.OpenAsync();

                var sql = "SELECT 1";
                await using var command = new MySqlCommand(sql, connection);
                await using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    Console.WriteLine(reader.GetInt32(0));
                }

                Console.WriteLine(" DB test succeeded.");
            }
            catch (Exception e)
            {
                Console.WriteLine("DB test failed: " + e);
            }
        }
    }
}
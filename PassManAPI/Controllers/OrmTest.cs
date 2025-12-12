using Microsoft.EntityFrameworkCore;
using PassManAPI.Data;

namespace PassManAPI.Controllers
{
    public interface IDatabaseHealthService
    {
        Task<bool> CheckDatabaseConnectionAsync();
        Task<string> GetDatabaseStatusAsync();
    }

    public class DatabaseHealthService : IDatabaseHealthService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<DatabaseHealthService> _logger;

        public DatabaseHealthService(
            ApplicationDbContext context,
            ILogger<DatabaseHealthService> logger
        )
        {
            _context = context;
            _logger = logger;
        }

        public async Task<bool> CheckDatabaseConnectionAsync()
        {
            try
            {
                var canConnect = await _context.Database.CanConnectAsync();
                if (canConnect)
                {
                    _logger.LogInformation("Database connection test passed");
                    return true;
                }
                else
                {
                    _logger.LogWarning("Database connection test returned false");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database connection test failed");
                return false;
            }
        }

        public async Task<string> GetDatabaseStatusAsync()
        {
            try
            {
                var canConnect = await _context.Database.CanConnectAsync();
                var providerName = _context.Database.ProviderName;
                var databaseName = _context.Database.GetDbConnection().Database;

                return $"Database: {databaseName}, Provider: {providerName}, Status: {(canConnect ? "Connected" : "Disconnected")}";
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }
    }
}

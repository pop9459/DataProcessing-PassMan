using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using PassManAPI.Data;
using PassManAPI.Models;

namespace PassManAPI.Tests;

/// <summary>
/// Test factory that runs the API against an in-memory SQLite database and "Test" environment,
/// so we avoid hitting MySQL and dev-only behaviors.
/// </summary>
public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        builder.ConfigureServices(services =>
        {
            // Replace ApplicationDbContext with a shared in-memory SQLite connection
            services.RemoveAll(typeof(DbContextOptions<ApplicationDbContext>));
            services.RemoveAll(typeof(ApplicationDbContext));

            var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();

            services.AddSingleton(connection);
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseSqlite(connection);
                options.EnableSensitiveDataLogging();
            });

            // Build service provider to apply schema
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Database.EnsureCreated();

            // Seed roles/permissions so authorization policies can be tested.
            DbSeeder.SeedAsync(scope.ServiceProvider, seedDemoUsers: true).GetAwaiter().GetResult();
        });

        return base.CreateHost(builder);
    }
}


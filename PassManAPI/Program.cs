namespace PassManAPI;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PassManAPI.Data;
using PassManAPI.Models;
using PassManAPI.Controllers;
using PassManAPI.Helpers;
using PassManAPI.Managers;
using PassManAPI.Components;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents(); // Blazor components
        builder.Services.AddControllers();          // Register MVC controllers
        builder.Services.AddEndpointsApiExplorer(); // Enable API explorer for minimal API metadata
        builder.Services.AddSwaggerGen(options =>
        {
            var xmlFilename =
                $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
            options.IncludeXmlComments(
                System.IO.Path.Combine(AppContext.BaseDirectory, xmlFilename)
            );
        });

        // Add the DB Context
        builder.Services.AddDbContext<ApplicationDbContext>(options =>
            options.UseMySql(
                builder.Configuration.GetConnectionString("DefaultConnection"),
                new MySqlServerVersion(new Version(8, 0, 0))
            )
        );

        // Add Identity services
        builder.Services.AddIdentity<User, IdentityRole<int>>(options =>
        {
            // Password settings
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequiredLength = 8;

            // Lockout settings
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.AllowedForNewUsers = true;

            // User settings
            options.User.RequireUniqueEmail = true;

            // Sign-in settings
            options.SignIn.RequireConfirmedEmail = true;
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

        var app = builder.Build();

        using (var scope = app.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            try
            {
                // Test the database connection
                var canConnect = await dbContext.Database.CanConnectAsync();
                if (canConnect)
                {
                    Console.WriteLine("Database connection via EF Core successful!");
                }
                else
                {
                    Console.WriteLine("Database connection test via EF Core returned false");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Database connection test via EF Core failed: {ex.Message}");
            }
        }

        if (app.Environment.IsDevelopment())
        {
            // Call DB to test the connectivity
            var conn = builder.Configuration.GetConnectionString("DefaultConnection")
                       ?? "Server=db;Port=3306;Database=passManDB;User=root;Password=hihi";
            await SqlTest.RunAsync(conn);

            // Seed the database with test data
            using var seedScope = app.Services.CreateScope();
            await DbSeeder.SeedAsync(seedScope.ServiceProvider);
        }

        // Enable swagger UI in development environment
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error", createScopeForErrors: true);
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }
        app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
        app.UseHttpsRedirection();

        // Authentication & Authorization middleware
        app.UseAuthentication();
        app.UseAuthorization();

        app.UseAntiforgery();

        // Map controller routes BEFORE Blazor to prioritize API endpoints
        app.MapControllers();

        app.MapStaticAssets();
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();

        app.Run();
    }
}

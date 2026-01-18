namespace PassManAPI;

using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PassManAPI.Data;
using PassManAPI.Models;
using PassManAPI.Controllers;
using PassManAPI.Helpers;
using PassManAPI.Managers;
using PassManAPI.Middleware;
using PassManAPI.Services;
using PassManAPI.Validators;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
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

        // Add the DB Context (use Sqlite for tests, MySQL otherwise)
        if (builder.Environment.IsEnvironment("Test"))
        {
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlite("DataSource=:memory:"));
        }
        else
        {
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseMySql(
                    builder.Configuration.GetConnectionString("DefaultConnection"),
                    new MySqlServerVersion(new Version(8, 0, 0))
                )
            );
        }

        // Add DB Health Service
        builder.Services.AddScoped<IDatabaseHealthService, DatabaseHealthService>();

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

        // Authentication/Authorization (place after AddIdentity so defaults are not overridden by Identity)
        builder.Services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = DevHeaderAuthenticationHandler.Scheme;
                options.DefaultChallengeScheme = DevHeaderAuthenticationHandler.Scheme;
            })
            .AddScheme<AuthenticationSchemeOptions, DevHeaderAuthenticationHandler>(
                DevHeaderAuthenticationHandler.Scheme,
                _ => { });

        builder.Services.AddAuthorization(options =>
        {
            foreach (var permission in PermissionConstants.All)
            {
                options.AddPolicy(permission, policy =>
                    policy.RequireClaim(PermissionConstants.ClaimType, permission));
            }
        });

        // Configure CORS for frontend
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowFrontend", policy =>
            {
                policy.WithOrigins(
                        "http://localhost:5247",      // PassManGUI Docker port
                        "http://localhost:5127",      // PassManGUI local dev port
                        "http://passman-gui:8080"     // Docker internal network
                      )
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials();
            });
        });
        
        // Use BCrypt for password hashing and expose lightweight user manager
        builder.Services.AddScoped<IPasswordHasher<User>, BCryptPasswordHasher>();
        builder.Services.AddScoped<PassManAPI.Managers.UserManager>();

        // Register VaultManager for vault business logic
        builder.Services.AddScoped<IVaultManager, VaultManager>();

        // Register Security Services
        // JWT Token Service
        builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SectionName));
        builder.Services.AddScoped<ITokenService, TokenService>();

        // Password Encryption Service (AES-256-GCM)
        builder.Services.AddSingleton<IPasswordEncryptionService, PasswordEncryptionService>();

        // Two-Factor Authentication Service (TOTP)
        builder.Services.AddSingleton<ITwoFactorService, TwoFactorService>();

        // Breach Check Service (Have I Been Pwned)
        builder.Services.Configure<BreachCheckSettings>(builder.Configuration.GetSection(BreachCheckSettings.SectionName));
        builder.Services.AddHttpClient<IBreachCheckService, BreachCheckService>();

        // Register Business Managers
        builder.Services.AddScoped<ISharingManager, SharingManager>();
        builder.Services.AddScoped<IAuthManager, AuthManager>();
        builder.Services.AddScoped<IAuditService, AuditManager>();

        // FluentValidation - auto-validate request models
        builder.Services.AddFluentValidationAutoValidation();
        builder.Services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();

        var app = builder.Build();

        // Global exception handler middleware (must be early in pipeline)
        app.UseGlobalExceptionHandler();

        using (var scope = app.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            try
            {
                // Apply pending migrations so Identity + domain tables exist
                await dbContext.Database.MigrateAsync();

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
        }

        // Always ensure core roles/permissions exist; seed demo users only in dev.
        using (var seedScope = app.Services.CreateScope())
        {
            await DbSeeder.SeedAsync(
                seedScope.ServiceProvider,
                seedDemoUsers: app.Environment.IsDevelopment()
            );
            await DatabaseArtifacts.EnsureAsync(seedScope.ServiceProvider);
        }

        // Enable swagger UI in development environment
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();

        // Enable CORS
        app.UseCors("AllowFrontend");

        // Authentication & Authorization middleware
        app.UseAuthentication();
        app.UseAuthorization();

        // Map controller routes (API only)
        app.MapControllers();

        app.Run();
    }
}

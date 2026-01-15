namespace PassManAPI;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PassManAPI.Data;
using PassManAPI.Models;
using PassManAPI.Controllers;
using PassManAPI.Helpers;
using PassManAPI.Managers;
using PassManAPI.Services;
using System.Text;

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
            // Enable JWT bearer auth in Swagger UI.
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Enter: Bearer {token}"
            });
            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });
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

        // JWT configuration + auth setup
        var jwtSection = builder.Configuration.GetSection("Jwt");
        builder.Services.Configure<JwtOptions>(jwtSection);
        var jwtOptions = jwtSection.Get<JwtOptions>() ?? new JwtOptions();

        builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            // Validate issuer/audience/signature for every request.
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtOptions.Issuer,
                ValidAudience = jwtOptions.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtOptions.SigningKey)
                ),
                ClockSkew = TimeSpan.FromMinutes(1)
            };
        });

        builder.Services.AddAuthorization();
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
        builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();

        var app = builder.Build();

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

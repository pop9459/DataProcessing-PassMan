namespace PassManAPI;

using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using PassManAPI.Data;
using PassManAPI.Models;
using PassManAPI.Controllers;
using PassManAPI.Helpers;
using PassManAPI.Managers;
using PassManAPI.Middleware;
using PassManAPI.Services;
using PassManAPI.Validators;
using PassManAPI.DTOs;
using System.Linq;
using Microsoft.OpenApi;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddControllers()           // Register MVC controllers
            .AddXmlSerializerFormatters();          // Enable XML serialization support

        // Standardize automatic model-validation (400) responses to the ErrorResponse shape
        // so validation errors match every other error in the API (see #162/#163).
        builder.Services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var traceId = System.Diagnostics.Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
                var errors = context.ModelState
                    .Where(kvp => kvp.Value is not null && kvp.Value.Errors.Count > 0)
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray());
                var error = ErrorResponse.ValidationError(errors, traceId);
                // No explicit ContentTypes: let content negotiation pick JSON or XML.
                return new Microsoft.AspNetCore.Mvc.ObjectResult(error)
                {
                    StatusCode = StatusCodes.Status400BadRequest
                };
            };
        });
        builder.Services.AddEndpointsApiExplorer(); // Enable API explorer for minimal API metadata
        builder.Services.AddSwaggerGen(options =>
        {
            var xmlFilename =
                $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
            options.IncludeXmlComments(
                System.IO.Path.Combine(AppContext.BaseDirectory, xmlFilename)
            );

            // Make the JWT bearer token usable from the Swagger UI "Authorize" button.
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Paste your JWT access token from /api/auth/login. Swagger adds the \"Bearer \" prefix automatically."
            });
            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", document)] = []
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
                .AddInterceptors(new ReadCommittedInterceptor())
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

        // JWT configuration
        var jwtSection = builder.Configuration.GetSection("Jwt");
        builder.Services.Configure<JwtOptions>(jwtSection);
        var jwtOptions = jwtSection.Get<JwtOptions>() ?? new JwtOptions();

        // Authentication: JWT Bearer as primary, with DevHeader for test backward compat
        // Use policy scheme in Test environment to auto-select based on Authorization header
        var isTestEnv = builder.Environment.IsEnvironment("Test");
        
        builder.Services
            .AddAuthentication(options =>
            {
                if (isTestEnv)
                {
                    // In Test, use a policy that selects scheme based on Authorization header
                    options.DefaultAuthenticateScheme = "SmartScheme";
                    options.DefaultChallengeScheme = "SmartScheme";
                }
                else
                {
                    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                }
            })
            .AddJwtBearer(options =>
            {
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
            })
            .AddScheme<AuthenticationSchemeOptions, DevHeaderAuthenticationHandler>(
                DevHeaderAuthenticationHandler.Scheme,
                _ => { })
            .AddPolicyScheme("SmartScheme", "Smart Auth Scheme", options =>
            {
                options.ForwardDefaultSelector = context =>
                {
                    // If Authorization header contains Bearer token, use JWT
                    var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
                    if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    {
                        return JwtBearerDefaults.AuthenticationScheme;
                    }
                    // Otherwise fall back to DevHeader for test backward compatibility
                    return DevHeaderAuthenticationHandler.Scheme;
                };
            });

        // JWT Token Service for generating tokens
        builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();

        builder.Services.AddAuthorization(options =>
        {
            foreach (var permission in PermissionConstants.All)
            {
                options.AddPolicy(permission, policy =>
                {
                    policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme, DevHeaderAuthenticationHandler.Scheme);
                    policy.RequireAuthenticatedUser();
                    policy.RequireClaim(PermissionConstants.ClaimType, permission);
                });
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
        // Password Encryption Service (AES-256-GCM)
        builder.Services.Configure<EncryptionOptions>(builder.Configuration.GetSection(EncryptionOptions.SectionName));
        builder.Services.AddSingleton<IPasswordEncryptionService, PasswordEncryptionService>();

        // Two-Factor Authentication Service (TOTP)
        builder.Services.AddSingleton<ITwoFactorService, TwoFactorService>();

        // Breach Check Service (Have I Been Pwned)
        builder.Services.Configure<BreachCheckSettings>(builder.Configuration.GetSection(BreachCheckSettings.SectionName));
        builder.Services.AddHttpClient<IBreachCheckService, BreachCheckService>();

        // Register Business Managers
        builder.Services.AddScoped<ISharingManager, SharingManager>();
        builder.Services.AddScoped<ICredentialManager, CredentialManager>();
        builder.Services.AddScoped<IAuditService, AuditManager>();

        // FluentValidation - auto-validate request models
        builder.Services.AddFluentValidationAutoValidation();
        builder.Services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();

        var app = builder.Build();

        // Global exception handler middleware (must be early in pipeline)
        app.UseGlobalExceptionHandler();

        // Give bodyless 401/403 responses from the auth pipeline a standardized ErrorResponse body.
        app.UseAuthErrorBody();

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
                       ?? "Server=db;Port=3306;Database=passManDB;User=passman_app;Password=passman_app_dev_pw";
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

        if (!app.Environment.IsEnvironment("Test"))
        {
            app.UseHttpsRedirection();
        }

        // Enable CORS
        app.UseCors("AllowFrontend");

        // Authentication & Authorization middleware
        app.UseAuthentication();
        app.UseAuthorization();

        // Map controller routes (API only)
        app.MapControllers();

        // Unauthenticated liveness probe — used by Docker healthcheck and Newman depends_on.
        app.MapGet("/health", () => Results.Ok()).AllowAnonymous();

        app.Run();
    }
}

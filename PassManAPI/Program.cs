namespace PassManAPI;

using Microsoft.EntityFrameworkCore;
using PassManAPI.Data;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddControllers(); // Register MVC controllers
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
                Console.WriteLine("Database connection test via EF Core failed", ex.Message);
            }
        }

        if (app.Environment.IsDevelopment())
        {
            // Enable swagger UI
            app.UseSwagger();
            app.UseSwaggerUI();

            // Call DB to test the connectivity
            var conn =
                builder.Configuration.GetConnectionString("DefaultConnection")
                ?? "Server=db;Port=3306;Database=passManDB;User=root;Password=hihi";
            await PassManAPI.Controllers.SqlTest.RunAsync(conn);
        }

        // Root endpoint - display welcome message
        app.MapGet(
            "/",
            () =>
                " _______                               __       __                               ______   _______   ______\n"
                + "/       \\                             /  \\     /  |                             /      \\ /       \\ /      |\n"
                + "$$$$$$$  | ______    _______  _______ $$  \\   /$$ |  ______   _______          /$$$$$$  |$$$$$$$  |$$$$$$/\n"
                + "$$ |__$$ |/      \\  /       |/       |$$$  \\ /$$$ | /      \\ /       \\         $$ |__$$ |$$ |__$$ |  $$ |\n"
                + "$$    $$/ $$$$$$  |/$$$$$$$//$$$$$$$/ $$$$  /$$$$ | $$$$$$  |$$$$$$$  |        $$    $$ |$$    $$/   $$ |\n"
                + "$$$$$$$/  /    $$ |$$      \\$$      \\ $$ $$ $$/$$ | /    $$ |$$ |  $$ |        $$$$$$$$ |$$$$$$$/    $$ |\n"
                + "$$ |     /$$$$$$$ | $$$$$$  |$$$$$$  |$$ |$$$/ $$ |/$$$$$$$ |$$ |  $$ |        $$ |  $$ |$$ |       _$$ |_ \n"
                + "$$ |     $$    $$ |/     $$//     $$/ $$ | $/  $$ |$$    $$ |$$ |  $$ |        $$ |  $$ |$$ |      / $$   |\n"
                + "$$/       $$$$$$$/ $$$$$$$/ $$$$$$$/  $$/      $$/  $$$$$$$/ $$/   $$/         $$/   $$/ $$/       $$$$$$/ \n"
                + "\n"
                + "Welcome to PassManAPI!\n"
                + "Visit https://github.com/pop9459/DataProcessing-PassMan for more information.\n"
                + "\n"
                + $"Running in: {app.Environment.EnvironmentName} environment"
        );

        // Test endpoint to verify EF Core setup
        app.MapGet(
            "/test-ef",
            async (ApplicationDbContext dbContext) =>
            {
                try
                {
                    var canConnect = await dbContext.Database.CanConnectAsync();
                    return Results.Ok(
                        new
                        {
                            success = true,
                            message = "EF Core connection successful",
                            canConnect,
                            environment = app.Environment.EnvironmentName,
                        }
                    );
                }
                catch (Exception ex)
                {
                    return Results.Problem($"EF Core connection failed: {ex.Message}");
                }
            }
        );

        // Map controller routes defined in the Controllers folder
        app.MapControllers();

        app.Run();
    }
}

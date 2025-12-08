namespace PassManAPI;

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
            var xmlFilename = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
            options.IncludeXmlComments(System.IO.Path.Combine(AppContext.BaseDirectory, xmlFilename));
        });
        var app = builder.Build();

        // Call DB to test the connectivity
        if (app.Environment.IsDevelopment())
        {
            var conn = builder.Configuration.GetConnectionString("DefaultConnection")
                       ?? "Server=db;Port=3306;Database=passManDB;User=root;Password=hihi";
            await PassManAPI.Controllers.SqlTest.RunAsync(conn);
        }

        // Enable swagger UI in development environment
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        // Root endpoint - display welcome message
        app.MapGet("/", () =>
            " _______                               __       __                               ______   _______   ______\n" + 
            "/       \\                             /  \\     /  |                             /      \\ /       \\ /      |\n" +
            "$$$$$$$  | ______    _______  _______ $$  \\   /$$ |  ______   _______          /$$$$$$  |$$$$$$$  |$$$$$$/\n" +
            "$$ |__$$ |/      \\  /       |/       |$$$  \\ /$$$ | /      \\ /       \\         $$ |__$$ |$$ |__$$ |  $$ |\n" +
            "$$    $$/ $$$$$$  |/$$$$$$$//$$$$$$$/ $$$$  /$$$$ | $$$$$$  |$$$$$$$  |        $$    $$ |$$    $$/   $$ |\n" +
            "$$$$$$$/  /    $$ |$$      \\$$      \\ $$ $$ $$/$$ | /    $$ |$$ |  $$ |        $$$$$$$$ |$$$$$$$/    $$ |\n" +
            "$$ |     /$$$$$$$ | $$$$$$  |$$$$$$  |$$ |$$$/ $$ |/$$$$$$$ |$$ |  $$ |        $$ |  $$ |$$ |       _$$ |_ \n" +
            "$$ |     $$    $$ |/     $$//     $$/ $$ | $/  $$ |$$    $$ |$$ |  $$ |        $$ |  $$ |$$ |      / $$   |\n" +
            "$$/       $$$$$$$/ $$$$$$$/ $$$$$$$/  $$/      $$/  $$$$$$$/ $$/   $$/         $$/   $$/ $$/       $$$$$$/ \n" +
            "\n"+
            "Welcome to PassManAPI!\n" +
            "Visit https://github.com/pop9459/DataProcessing-PassMan for more information.\n" + 
            "\n" +
            $"Running in: {app.Environment.EnvironmentName} environment"
        );


        // Map controller routes defined in the Controllers folder
        app.MapControllers();

        app.Run();
    }
}
namespace PassManAPI;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        builder.Services.AddOpenApi();

        var app = builder.Build();


        // Enable swagger UI in development environment
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        // Root endpoint - display welcome message
        app.MapGet("/", () =>
            " ____               __  __                  _    ____ ___     \n" +
            "|  _ \\ __ _ ___ ___|  \\/  | __ _ _ __      / \\  |  _ \\_ _|\n" +
            "| |_) / _` / __/ __| |\\/| |/ _` | '_ \\    / _ \\ | |_) | |  \n" +
            "|  __/ (_| \\__ \\__ \\ |  | | (_| | | | |  / ___ \\|  __/| | \n" +
            "|_|   \\__,_|___/___/_|  |_|\\__,_|_| |_| /_/   \\_\\_|  |___|\n" +
            "\n"+
            "Welcome to PassManAPI!\n" +
            "Visit https://github.com/pop9459/DataProcessing-PassMan for more information.\n" + 
            "\n" +
            $"Running in: {app.Environment.EnvironmentName} environment"
        );

        app.Run();
    }
}
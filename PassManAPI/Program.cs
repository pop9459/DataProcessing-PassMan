namespace PassManAPI;

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

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error", createScopeForErrors: true);
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }
        app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
        app.UseHttpsRedirection();

        app.UseAntiforgery();

        // Map controller routes BEFORE Blazor to prioritize API endpoints
        app.MapControllers();

        app.MapStaticAssets();
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();

        app.Run();
    }
}
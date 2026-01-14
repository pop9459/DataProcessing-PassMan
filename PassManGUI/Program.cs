using PassManGUI.Components;
using PassManGUI.Services;
using Microsoft.AspNetCore.Authentication;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Configure HttpClient to call the API
builder.Services.AddHttpClient("PassManAPI", client =>
{
    // Use environment variable or fallback to localhost for development
    var apiUrl = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5246";
    client.BaseAddress = new Uri(apiUrl);
});

// Register a typed HttpClient for dependency injection
builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("PassManAPI"));

// Register API services
builder.Services.AddScoped<IApiService, ApiService>();
builder.Services.AddScoped<AuthService>();

builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();

// Add Google Authentication
builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = Microsoft.AspNetCore.Authentication.Google.GoogleDefaults.AuthenticationScheme;
    })
    .AddCookie()
    .AddGoogle(options =>
    {
        options.ClientId = builder.Configuration["Authentication:Google:ClientId"] ?? "YOUR_GOOGLE_CLIENT_ID";
        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"] ?? "YOUR_GOOGLE_CLIENT_SECRET";
        options.SaveTokens = true;

        // Explicitly request scopes to ensure id_token is returned and saved
        options.Scope.Add("openid");
        options.Scope.Add("email");
        options.Scope.Add("profile");

        options.Events.OnCreatingTicket = context =>
        {
            // Manually persist the id_token if the default SaveTokens behavior misses it
            if (context.TokenResponse.Response.RootElement.TryGetProperty("id_token", out var idTokenProp))
            {
                var idToken = idTokenProp.GetString();
                if (!string.IsNullOrEmpty(idToken))
                {
                    context.Properties.StoreTokens(new[]
                    {
                        new Microsoft.AspNetCore.Authentication.AuthenticationToken { Name = "id_token", Value = idToken },
                        new Microsoft.AspNetCore.Authentication.AuthenticationToken { Name = "access_token", Value = context.AccessToken }
                    });
                }
            }
            return Task.CompletedTask;
        };
    });

// Register Controllers (for Auth redirects)
builder.Services.AddControllers();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
// app.UseHttpsRedirection(); // Disabled for Docker HTTP-only support

app.UseAuthentication(); // Ensure Auth middleware is used
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapControllers(); // Map the LoginController

app.Run();

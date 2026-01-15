namespace PassManAPI.Services;

public class JwtOptions
{
    // Values are bound from configuration (appsettings.json) under "Jwt".
    public string Issuer { get; set; } = "PassManAPI";
    public string Audience { get; set; } = "PassManAPI";
    public string SigningKey { get; set; } = string.Empty;
    public int AccessTokenLifetimeMinutes { get; set; } = 60;
}
namespace PassManAPI.Services;

public class JwtOptions
{
    // Values are bound from configuration (appsettings.json) under "Jwt".
    public string Issuer { get; set; } = "PassManAPI";
    public string Audience { get; set; } = "PassManAPI";
    public string SigningKey { get; set; } = string.Empty;
    public int AccessTokenLifetimeMinutes { get; set; } = 60;
}

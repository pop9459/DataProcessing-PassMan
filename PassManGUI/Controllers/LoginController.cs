using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Mvc;
using PassManGUI.Services;
using System.Security.Claims;

namespace PassManGUI.Controllers
{
    [Route("login")]
    public class LoginController : Controller
    {
        private readonly ILogger<LoginController> _logger;
        private readonly AuthService _authService;

        public LoginController(ILogger<LoginController> logger, AuthService authService)
        {
            _logger = logger;
            _authService = authService;
        }

        [HttpGet("google")]
        public async Task LoginWithGoogle()
            => await HttpContext.ChallengeAsync(GoogleDefaults.AuthenticationScheme, new AuthenticationProperties
            {
                RedirectUri = Url.Action(nameof(GoogleCallback))
            });

        [HttpGet("google-callback")]
        public async Task<IActionResult> GoogleCallback()
        {
            var authenticateResult = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            if (!authenticateResult.Succeeded)
            {
                _logger.LogWarning("Google authentication failed.");
                return Redirect("/login?error=GoogleAuthFailed");
            }

            var claimsPrincipal = authenticateResult.Principal;
            if (claimsPrincipal == null)
            {
                 return Redirect("/login?error=NoConfig");
            }
            
            // Extract the Google Id Token (if available) or construct our own flow.
            // Note: Standard ASP.NET Core Google handler sets cookies. We can get the ID Token if configured to save tokens.
            var idToken = authenticateResult.Properties?.GetTokenValue("id_token");
        
            if (string.IsNullOrEmpty(idToken))
            {
                 // Collect available keys for debugging
                 var debugInfo = authenticateResult.Properties != null 
                    ? string.Join(",", authenticateResult.Properties.Items.Keys) 
                    : "PropsNull";
                 
                 _logger.LogWarning("Google ID Token not found. Keys: {Keys}", debugInfo);
                 return Redirect($"/login?error=NoToken_Keys_{debugInfo}");
            }

            // Exchange Google Token for App Token
            var result = await _authService.LoginWithGoogleAsync(idToken);

            if (result.Success)
            {
                // We are in a Controller, so strictly speaking "sessionStorage" (JS) is not accessible directly here.
                // However, our AuthService creates a token. We need to pass this to the Blazor app.
                // Common trick: Set a short-lived cookie or redirect with token in query param (less secure but works for "magic link" style).
                // BETTER: We can't use sessionStorage from here. 
                // We will redirect to a Blazor page that reads the token from the query string and saves it.
                
                return Redirect($"/login-check?token={result.Data.AccessToken}");
            }

            return Redirect($"/login?error={Uri.EscapeDataString(result.ErrorMessage ?? "Login failed")}");
        }
    }
}

using System.ComponentModel.DataAnnotations;

namespace PassManAPI.DTOs;

public class GoogleLoginRequest
{
    [Required]
    public string IdToken { get; set; } = string.Empty;
}

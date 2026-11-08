namespace TaxNavi.Domain.Models;

using Entities;

public class AuthenticationResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? JwtToken { get; set; }
    public int ExpiresIn { get; set; }
    public UserDto? User { get; set; }
}

namespace TaxNavi.Infrastructure.Services;

using System.IdentityModel.Tokens.Jwt;
using BCrypt.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

public class JwtSettings
{
    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = "TaxNavi.ai";
    public string Audience { get; set; } = "TaxNavi.Users";
    public int ExpirationMinutes { get; set; } = 720;

    public static JwtSettings FromConfiguration(IConfiguration configuration) => new()
    {
        Secret = configuration["Jwt:Secret"] ?? Environment.GetEnvironmentVariable("JWT_SECRET") ?? string.Empty,
        Issuer = configuration["Jwt:Issuer"] ?? Environment.GetEnvironmentVariable("JWT_ISSUER") ?? "TaxNavi.ai",
        Audience = configuration["Jwt:Audience"] ?? Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? "TaxNavi.Users",
        ExpirationMinutes = int.TryParse(configuration["Jwt:ExpirationMinutes"], out var minutes) && minutes > 0 ? minutes : 720
    };
}

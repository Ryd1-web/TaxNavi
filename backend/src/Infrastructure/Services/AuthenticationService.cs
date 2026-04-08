namespace TaxNavi.Infrastructure.Services;

using TaxNavi.Domain.Entities;
using TaxNavi.Domain.Interfaces;
using System.Security.Cryptography;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using BCrypt.Net;
using Microsoft.Extensions.Configuration;

public class AuthenticationService : IAuthenticationService
{
    private readonly IUserRepository _userRepository;
    private readonly string _jwtSecret;
    private readonly string _jwtIssuer;
    private readonly string _jwtAudience;

    public AuthenticationService(IUserRepository userRepository, IConfiguration configuration)
    {
        _userRepository = userRepository;
        _jwtSecret = configuration["Jwt:Secret"]
            ?? Environment.GetEnvironmentVariable("JWT_SECRET")
            ?? "your-super-secret-key-that-is-at-least-32-characters-long-change-in-production";
        _jwtIssuer = configuration["Jwt:Issuer"]
            ?? Environment.GetEnvironmentVariable("JWT_ISSUER")
            ?? "TaxNavi.ai";
        _jwtAudience = configuration["Jwt:Audience"]
            ?? Environment.GetEnvironmentVariable("JWT_AUDIENCE")
            ?? "TaxNavi.Users";
    }

    public async Task<AuthenticationResponse> RegisterAsync(RegisterRequest request)
    {
        // Check if user exists
        var existingUser = await _userRepository.GetByEmailAsync(request.Email);
        if (existingUser != null)
        {
            return new AuthenticationResponse
            {
                Success = false,
                Message = "User with this email already exists"
            };
        }

        // Create new user
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            UserType = request.UserType,
            PasswordHash = BCrypt.HashPassword(request.Password),
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        await _userRepository.AddAsync(user);

        var token = GenerateJwtToken(user);
        var refreshToken = GenerateRefreshToken();

        return new AuthenticationResponse
        {
            Success = true,
            Message = "Registration successful",
            JwtToken = token,
            RefreshToken = refreshToken,
            User = new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                UserType = user.UserType
            },
            Data = new AuthenticationData
            {
                UserId = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                JwtToken = token,
                RefreshToken = refreshToken,
                ExpiresIn = 86400
            }
        };
    }

    public async Task<AuthenticationResponse> LoginAsync(LoginRequest request)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email);

        if (user == null || !BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return new AuthenticationResponse
            {
                Success = false,
                Message = "Invalid email or password"
            };
        }

        if (!user.IsActive)
        {
            return new AuthenticationResponse
            {
                Success = false,
                Message = "User account is inactive"
            };
        }

        var token = GenerateJwtToken(user);
        var refreshToken = GenerateRefreshToken();

        return new AuthenticationResponse
        {
            Success = true,
            Message = "Login successful",
            JwtToken = token,
            RefreshToken = refreshToken,
            User = new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                UserType = user.UserType
            },
            Data = new AuthenticationData
            {
                UserId = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                JwtToken = token,
                RefreshToken = refreshToken,
                ExpiresIn = 86400
            }
        };
    }

    public async Task<TokenRefreshResponse> RefreshTokenAsync(string refreshToken)
    {
        // In a production system, you'd validate the refresh token against a stored token
        // For this implementation, we regenerate based on the validity of the request

        return await Task.FromResult(new TokenRefreshResponse
        {
            Success = true,
            Message = "Token refreshed successfully",
            Data = new TokenRefreshData
            {
                JwtToken = "new-jwt-token", // This would be properly implemented
                RefreshToken = GenerateRefreshToken(),
                ExpiresIn = 3600
            }
        });
    }

    public string GenerateJwtToken(User user)
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSecret));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.GivenName, user.FirstName),
            new Claim(ClaimTypes.Surname, user.LastName),
            new Claim("UserType", user.UserType)
        };

        var token = new JwtSecurityToken(
            issuer: _jwtIssuer,
            audience: _jwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(24),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var randomNumber = new byte[64];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }
    }
}

namespace TaxNavi.Infrastructure.Services;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BCrypt.Net;
using Microsoft.IdentityModel.Tokens;
using TaxNavi.Domain.Entities;
using TaxNavi.Domain.Interfaces;
using TaxNavi.Domain.Models;

public class AuthenticationService : IAuthenticationService
{
    private readonly IUserRepository _userRepository;
    private readonly JwtSettings _jwt;

    public AuthenticationService(IUserRepository userRepository, JwtSettings jwt)
    {
        _userRepository = userRepository;
        _jwt = jwt;
    }

    public async Task<AuthenticationResponse> RegisterAsync(RegisterRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await _userRepository.GetByEmailAsync(email) != null)
        {
            return new AuthenticationResponse { Success = false, Message = "An account with this email already exists. Try signing in instead." };
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            UserType = request.UserType,
            PasswordHash = BCrypt.HashPassword(request.Password),
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        await _userRepository.AddAsync(user);
        return SuccessFor(user, "Registration successful");
    }

    public async Task<AuthenticationResponse> LoginAsync(LoginRequest request)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email.Trim());

        // Same message for unknown email and wrong password to avoid account enumeration.
        if (user == null || !BCrypt.Verify(request.Password, user.PasswordHash) || !user.IsActive)
        {
            return new AuthenticationResponse { Success = false, Message = "Incorrect email or password." };
        }

        return SuccessFor(user, "Login successful");
    }

    public string GenerateJwtToken(User user)
    {
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Secret)), SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.GivenName, user.FirstName),
            new Claim(ClaimTypes.Surname, user.LastName),
            new Claim("UserType", user.UserType),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwt.ExpirationMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private AuthenticationResponse SuccessFor(User user, string message) => new()
    {
        Success = true,
        Message = message,
        JwtToken = GenerateJwtToken(user),
        ExpiresIn = _jwt.ExpirationMinutes * 60,
        User = new UserDto
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            UserType = user.UserType
        }
    };
}

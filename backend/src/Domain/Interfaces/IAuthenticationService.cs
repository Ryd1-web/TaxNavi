namespace TaxNavi.Domain.Interfaces;

using Entities;
using TaxNavi.Domain.Models;

public interface IAuthenticationService
{
    Task<AuthenticationResponse> RegisterAsync(RegisterRequest request);
    Task<AuthenticationResponse> LoginAsync(LoginRequest request);
    string GenerateJwtToken(User user);
}

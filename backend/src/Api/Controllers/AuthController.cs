using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TaxNavi.Domain.Interfaces;
using TaxNavi.Domain.Models;

namespace TaxNavi.Api.Controllers;

[Route("api/[controller]")]
[EnableRateLimiting("auth")]
public class AuthController : ApiControllerBase
{
    private readonly IAuthenticationService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthenticationService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var result = await _authService.RegisterAsync(request);
        if (!result.Success)
        {
            return Failure(StatusCodes.Status400BadRequest, result.Message);
        }

        _logger.LogInformation("New account registered {UserId}", result.User!.Id);
        return Success(SessionPayload(result), "Account created");
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _authService.LoginAsync(request);
        if (!result.Success)
        {
            return Failure(StatusCodes.Status401Unauthorized, result.Message);
        }

        return Success(SessionPayload(result), "Signed in");
    }

    [HttpGet("verify")]
    [Authorize]
    [DisableRateLimiting]
    public IActionResult VerifyToken() => Success(null, "Token is valid");

    private static object SessionPayload(AuthenticationResponse result) => new
    {
        userId = result.User!.Id,
        email = result.User.Email,
        firstName = result.User.FirstName,
        lastName = result.User.LastName,
        userType = result.User.UserType,
        jwtToken = result.JwtToken,
        expiresIn = result.ExpiresIn
    };
}

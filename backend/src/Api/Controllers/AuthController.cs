using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using TaxNavi.Application.DTOs;
using TaxNavi.Domain.Interfaces;

namespace TaxNavi.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthenticationService _authService;
        private readonly IUserRepository _userRepository;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IAuthenticationService authService, IUserRepository userRepository, ILogger<AuthController> logger)
        {
            _authService = authService;
            _userRepository = userRepository;
            _logger = logger;
        }

        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            try
            {
                _logger.LogInformation("Register attempt for email: {Email}", request.Email);

                var result = await _authService.RegisterAsync(request);

                if (!result.Success)
                {
                    return BadRequest(new { success = false, message = result.Message });
                }

                return Ok(new
                {
                    success = true,
                    message = "User registered successfully",
                    data = new
                    {
                        userId = result.Data?.UserId,
                        email = result.Data?.Email,
                        userName = result.Data?.FirstName
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during registration");
                return StatusCode(500, new { success = false, message = "An error occurred during registration" });
            }
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                _logger.LogInformation("Login attempt for email: {Email}", request.Email);

                var result = await _authService.LoginAsync(request);

                if (!result.Success)
                {
                    return Unauthorized(new { success = false, message = result.Message });
                }

                return Ok(new
                {
                    success = true,
                    message = "Login successful",
                    data = new
                    {
                        userId = result.Data?.UserId,
                        email = result.Data?.Email,
                        jwtToken = result.Data?.JwtToken,
                        refreshToken = result.Data?.RefreshToken,
                        expiresIn = result.Data?.ExpiresIn
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login");
                return StatusCode(500, new { success = false, message = "An error occurred during login" });
            }
        }

        [HttpPost("refresh")]
        [AllowAnonymous]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            try
            {
                var result = await _authService.RefreshTokenAsync(request.RefreshToken ?? string.Empty);

                if (!result.Success)
                {
                    return Unauthorized(new { success = false, message = result.Message });
                }

                return Ok(new
                {
                    success = true,
                    message = "Token refreshed successfully",
                    data = new
                    {
                        jwtToken = result.Data?.JwtToken,
                        refreshToken = result.Data?.RefreshToken,
                        expiresIn = result.Data?.ExpiresIn
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing token");
                return StatusCode(500, new { success = false, message = "An error occurred while refreshing token" });
            }
        }

        [HttpGet("verify")]
        [Authorize]
        public IActionResult VerifyToken()
        {
            return Ok(new { success = true, message = "Token is valid" });
        }
    }
}

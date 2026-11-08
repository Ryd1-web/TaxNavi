using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaxNavi.Application.Services;
using TaxNavi.Domain.Interfaces;

namespace TaxNavi.Api.Controllers;

[Route("api/account")]
[Authorize]
public class AccountController : ApiControllerBase
{
    private readonly IUserRepository _users;
    private readonly IBillingService _billing;

    public AccountController(IUserRepository users, IBillingService billing)
    {
        _users = users;
        _billing = billing;
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var user = await _users.GetByIdAsync(userId);
        if (user == null || !user.IsActive)
        {
            return Unauthorized();
        }

        return Success(new
        {
            id = user.Id,
            email = user.Email,
            firstName = user.FirstName,
            lastName = user.LastName,
            userType = user.UserType,
            createdAt = user.CreatedAt
        });
    }

    [HttpPut("me")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName) ||
            request.FirstName.Length > 100 || request.LastName.Length > 100 ||
            request.UserType is not ("Individual" or "SME"))
        {
            return Failure(StatusCodes.Status400BadRequest, "Enter your first and last name and choose an account type.");
        }

        var user = await _users.GetByIdAsync(userId);
        if (user == null)
        {
            return Unauthorized();
        }

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.UserType = request.UserType;
        user.UpdatedAt = DateTime.UtcNow;
        await _users.UpdateAsync(user);

        return Success(new
        {
            id = user.Id,
            email = user.Email,
            firstName = user.FirstName,
            lastName = user.LastName,
            userType = user.UserType
        }, "Profile updated");
    }

    [HttpGet("entitlements")]
    public async Task<IActionResult> Entitlements()
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        return Success(await _billing.GetEntitlementsAsync(userId));
    }
}

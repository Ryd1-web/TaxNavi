using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace TaxNavi.Api.Controllers;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>The authenticated user's id from the JWT. Never accept a user id from the request body or route.</summary>
    protected bool TryGetUserId(out Guid userId)
    {
        userId = Guid.Empty;
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return claim != null && Guid.TryParse(claim.Value, out userId);
    }

    protected IActionResult Success(object? data, string? message = null) =>
        Ok(new { success = true, message, data });

    protected IActionResult Failure(int statusCode, string message, string? code = null) =>
        StatusCode(statusCode, new { success = false, message, code });
}

using System.Security.Claims;
using LiveSwitch.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LiveSwitch.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IAuthService _authService;

    public UsersController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpGet]
    public async Task<IActionResult> GetUsers()
    {
        var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(currentUserId))
        {
            return Unauthorized();
        }

        var users = await _authService.GetAllUsersExceptAsync(currentUserId);
        return Ok(users);
    }
}

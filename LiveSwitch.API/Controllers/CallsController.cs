using System.Security.Claims;
using LiveSwitch.API.DTOs;
using LiveSwitch.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LiveSwitch.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class CallsController : ControllerBase
{
    private readonly ICallService _callService;
    private readonly ILiveSwitchTokenService _tokenService;
    private readonly ILogger<CallsController> _logger;

    public CallsController(
        ICallService callService,
        ILiveSwitchTokenService tokenService,
        ILogger<CallsController> logger)
    {
        _callService = callService;
        _tokenService = tokenService;
        _logger = logger;
    }

    [HttpGet("config")]
    public IActionResult GetLiveSwitchConfig()
    {
        var config = _tokenService.GetPublicConfig();
        return Ok(new LiveSwitchConfigDto
        {
            ApplicationId = config.ApplicationId,
            GatewayUrl = config.GatewayUrl
        });
    }

    [HttpPost]
    public async Task<IActionResult> CreateCall([FromBody] CreateCallRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var userId = GetCurrentUserId();
        var call = await _callService.CreateCallAsync(userId, request);
        return CreatedAtAction(nameof(GetCall), new { id = call.Id }, call);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetCall(string id)
    {
        var userId = GetCurrentUserId();
        var authorized = await _callService.AuthorizeUserForCallAsync(id, userId);
        if (!authorized)
        {
            return Forbid();
        }

        var call = await _callService.GetCallByIdAsync(id);
        if (call == null)
        {
            return NotFound();
        }

        return Ok(call);
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetCallHistory()
    {
        var userId = GetCurrentUserId();
        var history = await _callService.GetCallHistoryAsync(userId);
        return Ok(history);
    }

    [HttpPost("{id}/join")]
    public async Task<IActionResult> JoinCall(string id)
    {
        var userId = GetCurrentUserId();
        var authorized = await _callService.AuthorizeUserForCallAsync(id, userId);
        if (!authorized)
        {
            return Forbid();
        }

        var call = await _callService.JoinCallAsync(id, userId);
        if (call == null)
        {
            return NotFound();
        }

        return Ok(call);
    }

    [HttpPost("{id}/leave")]
    public async Task<IActionResult> LeaveCall(string id)
    {
        var userId = GetCurrentUserId();
        var call = await _callService.LeaveCallAsync(id, userId);
        if (call == null)
        {
            return NotFound();
        }

        return Ok(call);
    }

    [HttpPost("{id}/reject")]
    public async Task<IActionResult> RejectCall(string id)
    {
        var userId = GetCurrentUserId();
        var call = await _callService.RejectCallAsync(id, userId);
        if (call == null)
        {
            return NotFound();
        }

        return Ok(call);
    }

    [HttpPost("{id}/end")]
    public async Task<IActionResult> EndCall(string id)
    {
        var userId = GetCurrentUserId();
        var authorized = await _callService.AuthorizeUserForCallAsync(id, userId);
        if (!authorized)
        {
            return Forbid();
        }

        var call = await _callService.EndCallAsync(id, userId);
        if (call == null)
        {
            return NotFound();
        }

        return Ok(call);
    }

    [HttpPost("token/register")]
    public IActionResult GenerateRegisterToken([FromBody] RegisterTokenRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // MUST derive user identity from trusted JWT claims
        var userId = GetCurrentUserId();
        var config = _tokenService.GetPublicConfig();

        var token = _tokenService.GenerateRegisterToken(
            userId,
            request.DeviceId,
            request.ClientId,
            roles: new[] { "user" },
            channelIds: request.ChannelIds?.ToArray()
        );

        return Ok(new TokenResponseDto
        {
            Token = token,
            ApplicationId = config.ApplicationId,
            GatewayUrl = config.GatewayUrl,
            UserId = userId,
            DeviceId = request.DeviceId,
            ClientId = request.ClientId,
            ExpiresInSeconds = config.TokenExpirySeconds
        });
    }

    [HttpPost("{id}/token/join")]
    public async Task<IActionResult> GenerateJoinToken(string id, [FromBody] JoinTokenRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // MUST derive user identity from trusted JWT claims
        var userId = GetCurrentUserId();

        // Verify channel membership and call permissions before issuing a join token!
        var authorized = await _callService.AuthorizeUserForCallAsync(id, userId);
        if (!authorized)
        {
            _logger.LogWarning("Unauthorized join token request for call {CallId} by user {UserId}", id, userId);
            return Forbid();
        }

        var call = await _callService.GetCallByIdAsync(id);
        if (call == null)
        {
            return NotFound(new { message = "Call not found" });
        }

        var config = _tokenService.GetPublicConfig();
        var token = _tokenService.GenerateJoinToken(
            userId,
            request.DeviceId,
            request.ClientId,
            call.ChannelId,
            canUpdate: false
        );

        return Ok(new TokenResponseDto
        {
            Token = token,
            ApplicationId = config.ApplicationId,
            GatewayUrl = config.GatewayUrl,
            UserId = userId,
            DeviceId = request.DeviceId,
            ClientId = request.ClientId,
            ChannelId = call.ChannelId,
            ExpiresInSeconds = config.TokenExpirySeconds
        });
    }

    private string GetCurrentUserId()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            throw new UnauthorizedAccessException("User is not authenticated");
        }
        return userId;
    }
}

using System.ComponentModel.DataAnnotations;
using LiveSwitch.API.Models;

namespace LiveSwitch.API.DTOs;

public class LoginRequestDto
{
    [Required]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public class RegisterRequestDto
{
    [Required, MinLength(3)]
    public string Username { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(2)]
    public string DisplayName { get; set; } = string.Empty;

    [Required, MinLength(6)]
    public string Password { get; set; } = string.Empty;
}

public class UserDto
{
    public string Id { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class AuthResponseDto
{
    public string Token { get; set; } = string.Empty;
    public UserDto User { get; set; } = null!;
}

public class CreateCallRequestDto
{
    public string Title { get; set; } = string.Empty;

    [Required]
    public CallType CallType { get; set; } = CallType.Video;

    public List<string> ParticipantUserIds { get; set; } = new();
}

public class CallParticipantDto
{
    public string UserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public ParticipantRole Role { get; set; }
    public ParticipantStatus Status { get; set; }
    public DateTime? JoinedAt { get; set; }
    public DateTime? LeftAt { get; set; }
}

public class CallDto
{
    public string Id { get; set; } = string.Empty;
    public string ChannelId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public CallType CallType { get; set; }
    public CallStatus Status { get; set; }
    public string InitiatorUserId { get; set; } = string.Empty;
    public string InitiatorName { get; set; } = string.Empty;
    public string InitiatorAvatar { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public int? DurationSeconds { get; set; }
    public List<CallParticipantDto> Participants { get; set; } = new();
}

public class RegisterTokenRequestDto
{
    [Required]
    public string DeviceId { get; set; } = string.Empty;

    [Required]
    public string ClientId { get; set; } = string.Empty;

    public List<string>? ChannelIds { get; set; }
}

public class JoinTokenRequestDto
{
    [Required]
    public string DeviceId { get; set; } = string.Empty;

    [Required]
    public string ClientId { get; set; } = string.Empty;
}

public class TokenResponseDto
{
    public string Token { get; set; } = string.Empty;
    public string ApplicationId { get; set; } = string.Empty;
    public string GatewayUrl { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string? ChannelId { get; set; }
    public int ExpiresInSeconds { get; set; }
}

public class LiveSwitchConfigDto
{
    public string ApplicationId { get; set; } = string.Empty;
    public string GatewayUrl { get; set; } = string.Empty;
}

public class CallMessageDto
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string CallId { get; set; } = string.Empty;
    public string SenderUserId { get; set; } = string.Empty;
    public string SenderDisplayName { get; set; } = string.Empty;
    public string SenderAvatarUrl { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class MediaStateUpdateDto
{
    public string UserId { get; set; } = string.Empty;
    public bool IsAudioMuted { get; set; }
    public bool IsVideoMuted { get; set; }
    public bool IsScreenSharing { get; set; }
}

using System.Text.Json.Serialization;

namespace LiveSwitch.API.Models;

public class User
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    
    [JsonIgnore]
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<CallParticipant> CallParticipants { get; set; } = new();
}

public enum CallType
{
    Audio = 0,
    Video = 1,
    Group = 2
}

public enum CallStatus
{
    Initiated = 0,
    Ringing = 1,
    InProgress = 2,
    Ended = 3,
    Missed = 4,
    Rejected = 5
}

public enum ParticipantRole
{
    Host = 0,
    Participant = 1
}

public enum ParticipantStatus
{
    Invited = 0,
    Joined = 1,
    Rejected = 2,
    Left = 3
}

public class Call
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ChannelId { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = string.Empty;
    public CallType CallType { get; set; } = CallType.Video;
    public CallStatus Status { get; set; } = CallStatus.Initiated;
    
    public string InitiatorUserId { get; set; } = string.Empty;
    public User? InitiatorUser { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public int? DurationSeconds { get; set; }

    public List<CallParticipant> Participants { get; set; } = new();
}

public class CallParticipant
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string CallId { get; set; } = string.Empty;
    public Call? Call { get; set; }

    public string UserId { get; set; } = string.Empty;
    public User? User { get; set; }

    public ParticipantRole Role { get; set; } = ParticipantRole.Participant;
    public ParticipantStatus Status { get; set; } = ParticipantStatus.Invited;

    public DateTime? JoinedAt { get; set; }
    public DateTime? LeftAt { get; set; }
}

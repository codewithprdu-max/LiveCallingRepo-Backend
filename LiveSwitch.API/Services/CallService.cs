using LiveSwitch.API.Data;
using LiveSwitch.API.DTOs;
using LiveSwitch.API.Hubs;
using LiveSwitch.API.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace LiveSwitch.API.Services;

public interface ICallService
{
    Task<CallDto> CreateCallAsync(string currentUserId, CreateCallRequestDto request);
    Task<CallDto?> GetCallByIdAsync(string callId);
    Task<bool> AuthorizeUserForCallAsync(string callId, string userId);
    Task<CallDto?> JoinCallAsync(string callId, string userId);
    Task<CallDto?> LeaveCallAsync(string callId, string userId);
    Task<CallDto?> RejectCallAsync(string callId, string userId);
    Task<CallDto?> EndCallAsync(string callId, string userId);
    Task<List<CallDto>> GetCallHistoryAsync(string userId);
}

public class CallService : ICallService
{
    private readonly AppDbContext _context;
    private readonly IHubContext<CallHub> _hubContext;
    private readonly ILogger<CallService> _logger;

    public CallService(AppDbContext context, IHubContext<CallHub> hubContext, ILogger<CallService> logger)
    {
        _context = context;
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task<CallDto> CreateCallAsync(string currentUserId, CreateCallRequestDto request)
    {
        var initiator = await _context.Users.FindAsync(currentUserId)
            ?? throw new InvalidOperationException("Initiator user not found");

        var channelId = $"channel_{Guid.NewGuid():N}";
        var call = new Call
        {
            ChannelId = channelId,
            Title = string.IsNullOrWhiteSpace(request.Title)
                ? $"{initiator.DisplayName}'s {(request.CallType == CallType.Audio ? "Audio Call" : request.CallType == CallType.Group ? "Group Call" : "Video Call")}"
                : request.Title,
            CallType = request.CallType,
            Status = CallStatus.Ringing,
            InitiatorUserId = currentUserId,
            CreatedAt = DateTime.UtcNow
        };

        // Add initiator as Host
        call.Participants.Add(new CallParticipant
        {
            UserId = currentUserId,
            Role = ParticipantRole.Host,
            Status = ParticipantStatus.Joined,
            JoinedAt = DateTime.UtcNow
        });

        // Add invited participants
        var invitedUserIds = request.ParticipantUserIds
            .Where(id => id != currentUserId)
            .Distinct()
            .ToList();

        foreach (var pId in invitedUserIds)
        {
            var userExists = await _context.Users.AnyAsync(u => u.Id == pId);
            if (userExists)
            {
                call.Participants.Add(new CallParticipant
                {
                    UserId = pId,
                    Role = ParticipantRole.Participant,
                    Status = ParticipantStatus.Invited
                });
            }
        }

        _context.Calls.Add(call);
        await _context.SaveChangesAsync();

        var callDto = await GetCallDtoAsync(call.Id);

        // Signal invited users via SignalR
        foreach (var pId in invitedUserIds)
        {
            var connections = CallHub.GetUserConnections(pId);
            if (connections.Count > 0)
            {
                await _hubContext.Clients.Clients(connections).SendAsync("ReceiveCallInvitation", callDto);
            }
        }

        _logger.LogInformation("Call created {CallId} with channel {ChannelId} by user {UserId}", call.Id, call.ChannelId, currentUserId);
        return callDto!;
    }

    public async Task<CallDto?> GetCallByIdAsync(string callId)
    {
        return await GetCallDtoAsync(callId);
    }

    public async Task<bool> AuthorizeUserForCallAsync(string callId, string userId)
    {
        var call = await _context.Calls
            .Include(c => c.Participants)
            .FirstOrDefaultAsync(c => c.Id == callId || c.ChannelId == callId);

        if (call == null) return false;

        // User must be initiator or listed participant
        if (call.InitiatorUserId == userId) return true;
        return call.Participants.Any(p => p.UserId == userId);
    }

    public async Task<CallDto?> JoinCallAsync(string callId, string userId)
    {
        var call = await _context.Calls
            .Include(c => c.Participants)
            .FirstOrDefaultAsync(c => c.Id == callId);

        if (call == null) return null;

        var participant = call.Participants.FirstOrDefault(p => p.UserId == userId);
        if (participant == null)
        {
            // If group call, allow user to join if authorized
            participant = new CallParticipant
            {
                CallId = call.Id,
                UserId = userId,
                Role = ParticipantRole.Participant,
                Status = ParticipantStatus.Joined,
                JoinedAt = DateTime.UtcNow
            };
            call.Participants.Add(participant);
        }
        else
        {
            participant.Status = ParticipantStatus.Joined;
            participant.JoinedAt = DateTime.UtcNow;
            participant.LeftAt = null;
        }

        if (call.Status == CallStatus.Ringing || call.Status == CallStatus.Initiated)
        {
            call.Status = CallStatus.InProgress;
            call.StartedAt ??= DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        var callDto = await GetCallDtoAsync(call.Id);

        // Notify room
        await _hubContext.Clients.Group($"call_{callId}").SendAsync("ParticipantJoined", new
        {
            CallId = call.Id,
            UserId = userId,
            Call = callDto
        });

        // Also inform initiator directly if was ringing
        var initiatorConns = CallHub.GetUserConnections(call.InitiatorUserId);
        if (initiatorConns.Count > 0)
        {
            await _hubContext.Clients.Clients(initiatorConns).SendAsync("CallAccepted", callDto);
        }
        await _hubContext.Clients.Group($"call_{callId}").SendAsync("CallAccepted", callDto);

        _logger.LogInformation("User {UserId} joined call {CallId}", userId, callId);
        return callDto;
    }

    public async Task<CallDto?> LeaveCallAsync(string callId, string userId)
    {
        var call = await _context.Calls
            .Include(c => c.Participants)
            .FirstOrDefaultAsync(c => c.Id == callId);

        if (call == null) return null;

        var participant = call.Participants.FirstOrDefault(p => p.UserId == userId);
        if (participant != null)
        {
            participant.Status = ParticipantStatus.Left;
            participant.LeftAt = DateTime.UtcNow;
        }

        // Check if all participants left or host left
        var activeParticipants = call.Participants.Count(p => p.Status == ParticipantStatus.Joined);
        if (activeParticipants == 0 || (participant?.Role == ParticipantRole.Host && call.CallType != CallType.Group))
        {
            call.Status = CallStatus.Ended;
            call.EndedAt = DateTime.UtcNow;
            if (call.StartedAt.HasValue)
            {
                call.DurationSeconds = (int)(call.EndedAt.Value - call.StartedAt.Value).TotalSeconds;
            }
        }

        await _context.SaveChangesAsync();
        var callDto = await GetCallDtoAsync(call.Id);

        await _hubContext.Clients.Group($"call_{callId}").SendAsync("ParticipantLeft", new
        {
            CallId = call.Id,
            UserId = userId,
            Call = callDto
        });

        _logger.LogInformation("User {UserId} left call {CallId}", userId, callId);
        return callDto;
    }

    public async Task<CallDto?> RejectCallAsync(string callId, string userId)
    {
        var call = await _context.Calls
            .Include(c => c.Participants)
            .FirstOrDefaultAsync(c => c.Id == callId);

        if (call == null) return null;

        var participant = call.Participants.FirstOrDefault(p => p.UserId == userId);
        if (participant != null)
        {
            participant.Status = ParticipantStatus.Rejected;
        }

        if (call.CallType != CallType.Group)
        {
            call.Status = CallStatus.Rejected;
            call.EndedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        var callDto = await GetCallDtoAsync(call.Id);

        // Notify initiator
        var initiatorConns = CallHub.GetUserConnections(call.InitiatorUserId);
        if (initiatorConns.Count > 0)
        {
            await _hubContext.Clients.Clients(initiatorConns).SendAsync("CallRejected", new
            {
                CallId = call.Id,
                UserId = userId,
                Call = callDto
            });
        }

        _logger.LogInformation("User {UserId} rejected call {CallId}", userId, callId);
        return callDto;
    }

    public async Task<CallDto?> EndCallAsync(string callId, string userId)
    {
        var call = await _context.Calls
            .Include(c => c.Participants)
            .FirstOrDefaultAsync(c => c.Id == callId);

        if (call == null) return null;

        call.Status = CallStatus.Ended;
        call.EndedAt = DateTime.UtcNow;
        if (call.StartedAt.HasValue)
        {
            call.DurationSeconds = (int)(call.EndedAt.Value - call.StartedAt.Value).TotalSeconds;
        }
        else
        {
            call.DurationSeconds = 0;
        }

        foreach (var p in call.Participants.Where(p => p.Status == ParticipantStatus.Joined))
        {
            p.Status = ParticipantStatus.Left;
            p.LeftAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        var callDto = await GetCallDtoAsync(call.Id);

        // Notify room and all participants
        await _hubContext.Clients.Group($"call_{callId}").SendAsync("CallEnded", callDto);
        foreach (var p in call.Participants)
        {
            var conns = CallHub.GetUserConnections(p.UserId);
            if (conns.Count > 0)
            {
                await _hubContext.Clients.Clients(conns).SendAsync("CallEnded", callDto);
            }
        }

        _logger.LogInformation("Call {CallId} ended by user {UserId}", callId, userId);
        return callDto;
    }

    public async Task<List<CallDto>> GetCallHistoryAsync(string userId)
    {
        var calls = await _context.Calls
            .Include(c => c.InitiatorUser)
            .Include(c => c.Participants)
                .ThenInclude(p => p.User)
            .Where(c => c.InitiatorUserId == userId || c.Participants.Any(p => p.UserId == userId))
            .OrderByDescending(c => c.CreatedAt)
            .Take(50)
            .ToListAsync();

        return calls.Select(MapCallToDto).ToList();
    }

    private async Task<CallDto?> GetCallDtoAsync(string callId)
    {
        var call = await _context.Calls
            .Include(c => c.InitiatorUser)
            .Include(c => c.Participants)
                .ThenInclude(p => p.User)
            .FirstOrDefaultAsync(c => c.Id == callId);

        return call == null ? null : MapCallToDto(call);
    }

    private static CallDto MapCallToDto(Call call)
    {
        return new CallDto
        {
            Id = call.Id,
            ChannelId = call.ChannelId,
            Title = call.Title,
            CallType = call.CallType,
            Status = call.Status,
            InitiatorUserId = call.InitiatorUserId,
            InitiatorName = call.InitiatorUser?.DisplayName ?? "Unknown",
            InitiatorAvatar = call.InitiatorUser?.AvatarUrl ?? "",
            CreatedAt = call.CreatedAt,
            StartedAt = call.StartedAt,
            EndedAt = call.EndedAt,
            DurationSeconds = call.DurationSeconds,
            Participants = call.Participants.Select(p => new CallParticipantDto
            {
                UserId = p.UserId,
                DisplayName = p.User?.DisplayName ?? "Unknown",
                AvatarUrl = p.User?.AvatarUrl ?? "",
                Role = p.Role,
                Status = p.Status,
                JoinedAt = p.JoinedAt,
                LeftAt = p.LeftAt
            }).ToList()
        };
    }
}

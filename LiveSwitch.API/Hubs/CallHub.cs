using System.Security.Claims;
using LiveSwitch.API.Data;
using LiveSwitch.API.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace LiveSwitch.API.Hubs;

[Authorize]
public class CallHub : Hub
{
    private static readonly Dictionary<string, HashSet<string>> UserConnections = new();
    private static readonly object LockObj = new();
    private readonly AppDbContext _context;
    private readonly ILogger<CallHub> _logger;

    public CallHub(AppDbContext context, ILogger<CallHub> logger)
    {
        _context = context;
        _logger = logger;
    }

    public override Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId))
        {
            lock (LockObj)
            {
                if (!UserConnections.ContainsKey(userId))
                {
                    UserConnections[userId] = new HashSet<string>();
                }
                UserConnections[userId].Add(Context.ConnectionId);
            }
            _logger.LogInformation("User {UserId} connected to CallHub on connection {ConnectionId}", userId, Context.ConnectionId);
        }

        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId))
        {
            lock (LockObj)
            {
                if (UserConnections.TryGetValue(userId, out var connections))
                {
                    connections.Remove(Context.ConnectionId);
                    if (connections.Count == 0)
                    {
                        UserConnections.Remove(userId);
                    }
                }
            }
            _logger.LogInformation("User {UserId} disconnected from CallHub", userId);
        }

        return base.OnDisconnectedAsync(exception);
    }

    public static List<string> GetUserConnections(string userId)
    {
        lock (LockObj)
        {
            return UserConnections.TryGetValue(userId, out var list) ? list.ToList() : new List<string>();
        }
    }

    public async Task JoinCallRoom(string callId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"call_{callId}");
    }

    public async Task LeaveCallRoom(string callId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"call_{callId}");
    }

    public async Task SendCallMessage(string callId, string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;

        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return;

        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
        var chatDto = new CallMessageDto
        {
            Id = Guid.NewGuid().ToString("N"),
            CallId = callId,
            SenderUserId = userId,
            SenderDisplayName = user?.DisplayName ?? Context.User?.FindFirst(ClaimTypes.Name)?.Value ?? "Anonymous",
            SenderAvatarUrl = user?.AvatarUrl ?? "",
            Message = message.Trim(),
            Timestamp = DateTime.UtcNow
        };

        await Clients.Group($"call_{callId}").SendAsync("ReceiveCallMessage", chatDto);
    }

    public async Task UpdateMediaState(string callId, bool isAudioMuted, bool isVideoMuted, bool isScreenSharing)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return;

        var update = new MediaStateUpdateDto
        {
            UserId = userId,
            IsAudioMuted = isAudioMuted,
            IsVideoMuted = isVideoMuted,
            IsScreenSharing = isScreenSharing
        };

        await Clients.Group($"call_{callId}").SendAsync("ParticipantMediaStateChanged", update);
    }
}

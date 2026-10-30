using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace LiveSwitch.API.Hubs;

[Authorize]
public class CallHub : Hub
{
    private static readonly Dictionary<string, HashSet<string>> UserConnections = new();
    private static readonly object LockObj = new();
    private readonly ILogger<CallHub> _logger;

    public CallHub(ILogger<CallHub> logger)
    {
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
}

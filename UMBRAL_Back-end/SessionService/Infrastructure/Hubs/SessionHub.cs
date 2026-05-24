namespace SessionService.Infrastructure.Hubs;

using Microsoft.AspNetCore.SignalR;

/// <summary>
/// Real-time hub for session state change notifications.
/// Clients join a session-specific group to receive updates only for that session.
/// </summary>
public class SessionHub : Hub
{
    /// <summary>
    /// Adds the caller to a SignalR group scoped to a specific session.
    /// Call this immediately after connecting, passing the session ID.
    /// </summary>
    public async Task JoinSession(string sessionId)
        => await Groups.AddToGroupAsync(Context.ConnectionId, sessionId);

    /// <summary>Removes the caller from the session group.</summary>
    public async Task LeaveSession(string sessionId)
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, sessionId);
}

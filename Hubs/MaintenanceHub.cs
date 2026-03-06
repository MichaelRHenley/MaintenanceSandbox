using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Linq;

namespace MaintenanceSandbox.Hubs
{
    public class MaintenanceHub : Hub
    {
        // connectionId -> (requestId, userName)
        private static readonly ConcurrentDictionary<string, (int RequestId, string UserName)> _presence
            = new();

        private static string GetGroupName(int requestId) => $"request-{requestId}";

        public async Task JoinRequestGroup(int requestId, string userName)
        {
            var connectionId = Context.ConnectionId;

            await Groups.AddToGroupAsync(connectionId, GetGroupName(requestId));

            // Store presence
            _presence[connectionId] = (requestId, userName ?? "Unknown");

            await BroadcastPresenceAsync(requestId);
        }

        public async Task LeaveRequestGroup(int requestId, string userName)
        {
            var connectionId = Context.ConnectionId;

            await Groups.RemoveFromGroupAsync(connectionId, GetGroupName(requestId));

            // Remove from presence
            _presence.TryRemove(connectionId, out _);

            await BroadcastPresenceAsync(requestId);
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            // If this connection was tracked, clean it up and broadcast
            if (_presence.TryRemove(Context.ConnectionId, out var info))
            {
                await BroadcastPresenceAsync(info.RequestId);
            }

            await base.OnDisconnectedAsync(exception);
        }

        private Task BroadcastPresenceAsync(int requestId)
        {
            // Get distinct responder names for this request
            var names = _presence
                .Where(kvp => kvp.Value.RequestId == requestId)
                .Select(kvp => kvp.Value.UserName)
                .Distinct()
                .OrderBy(n => n)
                .ToList();

            return Clients.Group(GetGroupName(requestId))
                .SendAsync("ResponderPresenceChanged", requestId, names);
        }

        public async Task StartTyping(int requestId, string displayName, string role)
        {
            var safeName = string.IsNullOrWhiteSpace(displayName) ? "Responder" : displayName;
            var label = string.IsNullOrWhiteSpace(role) ? safeName : $"{safeName} ({role})";

            await Clients.OthersInGroup(GetGroupName(requestId))
                .SendAsync("UserTyping", new
                {
                    requestId,
                    label,
                    isTyping = true
                });
        }

        public async Task StopTyping(int requestId, string displayName, string role)
        {
            var safeName = string.IsNullOrWhiteSpace(displayName) ? "Responder" : displayName;
            var label = string.IsNullOrWhiteSpace(role) ? safeName : $"{safeName} ({role})";

            await Clients.OthersInGroup(GetGroupName(requestId))
                .SendAsync("UserTyping", new
                {
                    requestId,
                    label,
                    isTyping = false
                });
        }

    }
}

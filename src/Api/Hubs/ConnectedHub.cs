using System.Collections.Concurrent;

namespace Api.Hubs
{
    public static class ConnectedUsers
    {
        private static readonly ConcurrentDictionary<int, string> _connections = new();

        public static void Add(int userId, string connectionId) => _connections[userId] = connectionId;

        public static void Remove(int userId) => _connections.TryRemove(userId, out _);

        public static string? GetConnectionId(int userId)
        {
            _connections.TryGetValue(userId, out var connectionId);
            return connectionId;
        }
    }
}

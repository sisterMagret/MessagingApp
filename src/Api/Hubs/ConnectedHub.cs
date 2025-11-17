using System.Collections.Concurrent;

namespace Api.Hubs
{
    public static class ConnectedUsers
    {
        private static readonly ConcurrentDictionary<int, string> _userConnections = new();
        private static readonly ConcurrentDictionary<string, int> _connectionUsers = new();

        public static void Add(int userId, string connectionId)
        {
            _userConnections[userId] = connectionId;
            _connectionUsers[connectionId] = userId;
        }

        public static void Remove(int userId)
        {
            if (_userConnections.TryRemove(userId, out var connectionId))
            {
                _connectionUsers.TryRemove(connectionId, out _);
            }
        }

        public static void RemoveByConnectionId(string connectionId)
        {
            if (_connectionUsers.TryRemove(connectionId, out var userId))
            {
                _userConnections.TryRemove(userId, out _);
            }
        }

        public static string? GetConnectionId(int userId)
        {
            _userConnections.TryGetValue(userId, out var connectionId);
            return connectionId;
        }

        public static int? GetUserId(string connectionId)
        {
            _connectionUsers.TryGetValue(connectionId, out var userId);
            return userId;
        }

        public static IEnumerable<int> GetAllConnectedUsers()
        {
            return _userConnections.Keys.ToList();
        }
    }
}
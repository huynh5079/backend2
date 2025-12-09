using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace TPEdu_API.Services
{
    /// <summary>
    /// Quản lý connections và online status của users
    /// </summary>
    public class ChatConnectionManager
    {
        // Dictionary: UserId -> List of ConnectionIds (một user có thể có nhiều connections từ nhiều devices)
        private static readonly ConcurrentDictionary<string, HashSet<string>> _userConnections = new();
        
        // Dictionary: ConnectionId -> UserId
        private static readonly ConcurrentDictionary<string, string> _connectionUsers = new();

        /// <summary>
        /// Thêm connection cho user
        /// </summary>
        public void AddConnection(string userId, string connectionId)
        {
            _userConnections.AddOrUpdate(
                userId,
                new HashSet<string> { connectionId },
                (key, existing) =>
                {
                    existing.Add(connectionId);
                    return existing;
                }
            );
            
            _connectionUsers.TryAdd(connectionId, userId);
        }

        /// <summary>
        /// Xóa connection của user
        /// </summary>
        public bool RemoveConnection(string connectionId)
        {
            if (_connectionUsers.TryRemove(connectionId, out var userId))
            {
                if (_userConnections.TryGetValue(userId, out var connections))
                {
                    connections.Remove(connectionId);
                    
                    // Nếu user không còn connection nào, xóa khỏi dictionary
                    if (connections.Count == 0)
                    {
                        _userConnections.TryRemove(userId, out _);
                        return true; // User đã offline
                    }
                }
            }
            return false; // User vẫn còn connections khác
        }

        /// <summary>
        /// Kiểm tra user có online không
        /// </summary>
        public bool IsUserOnline(string userId)
        {
            return _userConnections.ContainsKey(userId) && 
                   _userConnections[userId].Count > 0;
        }

        /// <summary>
        /// Lấy danh sách tất cả users online
        /// </summary>
        public List<string> GetOnlineUsers()
        {
            return _userConnections.Keys.ToList();
        }

        /// <summary>
        /// Lấy số lượng connections của user
        /// </summary>
        public int GetConnectionCount(string userId)
        {
            return _userConnections.TryGetValue(userId, out var connections) 
                ? connections.Count 
                : 0;
        }

        /// <summary>
        /// Lấy danh sách online users trong một list userIds
        /// </summary>
        public List<string> GetOnlineUsersFromList(List<string> userIds)
        {
            return userIds.Where(IsUserOnline).ToList();
        }
    }
}


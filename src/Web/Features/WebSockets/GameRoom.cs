using System.Net.WebSockets;
using System.Collections.Concurrent;

namespace WordRush.Web.Features.WebSockets
{
  public class GameRoom
  {
    public string RoomId { get; } = Guid.NewGuid().ToString();
    public string OwnerUserId { get; set; } = string.Empty;
    public List<WebSocket> Participants { get; } = new();
    public ConcurrentDictionary<string, bool> ReadyStatus { get; } = new();
    public ConcurrentDictionary<string, UserProfile> Profiles { get; } = new();

    public bool IsEmpty => Participants.Count == 0;
    public bool AllReady => ReadyStatus.Values.All(v => v);

    public void Add(WebSocket socket)
    {
      lock (Participants)
      {
        if (!Participants.Contains(socket))
          Participants.Add(socket);
      }
    }

    public void Remove(WebSocket socket)
    {
      lock (Participants)
      {
        Participants.Remove(socket);
      }
    }

    public List<string> GetUserIds(ConcurrentDictionary<WebSocket, string> userMap)
    {
      lock (Participants)
      {
        return Participants
            .Where(userMap.ContainsKey)
            .Select(s => userMap[s])
            .ToList();
      }
    }

    public void ToggleReady(string userId)
    {
      ReadyStatus[userId] = !ReadyStatus.GetValueOrDefault(userId, false);
    }

    public List<PlayerSnapshot> GetPlayerSnapshots()
    {
      return Profiles.Select(kv => new PlayerSnapshot
      {
        UserId = kv.Key,
        Nickname = kv.Value.Nickname,
        Avatar = kv.Value.Avatar,
        IsReady = ReadyStatus.GetValueOrDefault(kv.Key, false),
        IsOwner = kv.Key == OwnerUserId
      }).ToList();
    }
  }

  public class PlayerSnapshot
  {
    public string UserId { get; set; } = string.Empty;
    public string Nickname { get; set; } = "Player";
    public string Avatar { get; set; } = "";
    public bool IsReady { get; set; }
    public bool IsOwner { get; set; }
  }

  public class UserProfile
  {
    public string Nickname { get; set; } = "Player";
    public string Avatar { get; set; } = "";
    public string Email { get; set; } = "";
  }
}

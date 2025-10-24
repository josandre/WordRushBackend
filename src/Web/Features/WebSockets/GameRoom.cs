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

    // --- KEY FIX ---
    // This now ensures that even if a player doesn’t have a saved profile,
    // we still include them in the user list broadcast.
    public List<PlayerSnapshot> GetPlayerSnapshots()
    {
      // If Profiles is empty (e.g., right after joining),
      // we use ReadyStatus or default entries to populate the snapshot.
      var keys = Profiles.Keys.Any()
          ? Profiles.Keys
          : ReadyStatus.Keys;

      return keys.Select(userId =>
      {
        Profiles.TryGetValue(userId, out var profile);
        return new PlayerSnapshot
        {
          UserId = userId,
          Nickname = profile?.Nickname ?? $"Player-{userId[..5]}",
          Avatar = profile?.Avatar ?? "",
          IsReady = ReadyStatus.GetValueOrDefault(userId, false),
          IsOwner = userId == OwnerUserId
        };
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

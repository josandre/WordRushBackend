using System.Collections.Concurrent;
using System.Net.WebSockets;

namespace WordRush.Web.Features.WebSockets
{
  public class GameRoom
  {
    // --- Basic room info ---
    public int ID { get; set; } = 0;

    public bool Active { get; set; } = true;

    public bool InGame { get; set; } = false;

    public int CurrentRound { get; set; } = 0;

    public int TotalRounds { get; set; } = 7;

    // --- Rounds ---
    public List<Round> Rounds { get; set; } = new();

    // --- Letters ---
    public List<string> Letters { get; set; } = new();

    public LetterSelectionMode LetterSelection { get; set; } = LetterSelectionMode.InOrder;

    // --- Timing ---
    public int RoundTime { get; set; } = 60; // seconds

    public string StartTime { get; set; } = string.Empty;

    public string EndTime { get; set; } = string.Empty;

    // --- Ownership and players ---
    public string HostID { get; set; } = string.Empty;

    public List<Player> Players { get; set; } = new();

    // --- WebSocket data (runtime only) ---
    public string RoomId { get; } = Guid.NewGuid().ToString();

    public string OwnerUserId { get; set; } = string.Empty;

    public List<WebSocket> Participants { get; } = new();

    public ConcurrentDictionary<string, bool> ReadyStatus { get; } = new();

    public ConcurrentDictionary<string, UserProfile> Profiles { get; } = new();

    public bool IsEmpty => Participants.Count == 0;

    public bool AllReady => ReadyStatus.Values.All(v => v);

    // --- Methods ---
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
          Avatar = profile?.Avatar ?? string.Empty,
          IsReady = ReadyStatus.GetValueOrDefault(userId, false),
          IsOwner = userId == OwnerUserId
        };
      }).ToList();
    }
  }

  // --- Additional supporting classes ---
  public class Round
  {
    public Dictionary<string, List<string>> PlayerResponses { get; set; } = new();
  }

  public class Player
  {
    public string UserID { get; set; } = string.Empty;

    public int Score { get; set; } = 0;
  }

  public enum LetterSelectionMode
  {
    InOrder,
    Random
  }

  public class PlayerSnapshot
  {
    public string UserId { get; set; } = string.Empty;

    public string Nickname { get; set; } = "Player";

    public string Avatar { get; set; } = string.Empty;

    public bool IsReady { get; set; }

    public bool IsOwner { get; set; }
  }

  public class UserProfile
  {
    public string Nickname { get; set; } = "Player";

    public string Avatar { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;
  }
}

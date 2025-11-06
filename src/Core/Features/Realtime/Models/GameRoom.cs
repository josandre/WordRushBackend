using System.Collections.Concurrent;
using System.Net.WebSockets;
using WordRush.Core.Features.Realtime.Models;
using WordRush.Repository.Models;

public class GameRoom
{
  private readonly object _lock = new();

  public string RoomId { get; }

  public bool Active { get; set; } = true;

  public bool InGame { get; set; } = false;

  public GameSettings Settings { get; set; } = new();

  public string StartTime { get; set; } = string.Empty;

  public string EndTime { get; set; } = string.Empty;

  public string HostId { get; set; } = string.Empty;

  public List<User> Players { get; set; } = new();

  public List<WebSocket> Participants { get; } = new();

  public ConcurrentDictionary<string, bool> ReadyStatus { get; } = new();

  public ConcurrentDictionary<string, UserProfile> Profiles { get; } = new();

  public bool IsEmpty => Participants.Count == 0;

  public bool AllReady => ReadyStatus.Values.All(v => v);

  public GameRoom(string roomId)
  {
    RoomId = roomId;
  }

  public void AddParticipantSocket(WebSocket socket)
  {
    lock (_lock)
    {
      if (!Participants.Contains(socket))
      {
        Participants.Add(socket);
      }
    }
  }

  public void RemoveParticipantSocket(WebSocket socket)
  {
    lock (_lock)
    {
      _ = Participants.Remove(socket);
    }
  }

  public List<string> GetUserIds(ConcurrentDictionary<WebSocket, string> userMap)
  {
    lock (_lock)
    {
      return Participants
        .Where(userMap.ContainsKey)
        .Select(s => userMap[s])
        .ToList();
    }
  }

  public void ToggleReady(string userId)
  {
    lock (_lock)
    {
      ReadyStatus[userId] = !ReadyStatus.GetValueOrDefault(userId, false);
    }
  }

  public RoomDataRequestedEvent GetRoomData()
  {
    lock (_lock)
    {
      GameSettings currentSettings = Settings;

      ICollection<string> keys = Profiles.Keys.Any()
        ? Profiles.Keys
        : ReadyStatus.Keys;

      RoomDataRequestedEvent roomData = new()
      {
        Settings = new GameSettings
        {
          TimeLimit = currentSettings.TimeLimit,
          Order = currentSettings.Order,
          Letters = currentSettings.Letters != null ? currentSettings.Letters.ToArray() : Array.Empty<string>()
        }
      };

      IEnumerator<string> userIDs = Profiles.Keys.GetEnumerator();
      while (userIDs.MoveNext())
      {
        string userID = userIDs.Current;

        if (Profiles.TryGetValue(userID, out UserProfile profile))
        {
          roomData.Players.Add(new RoomDataPlayer
          {
            UserId = userID,
            Nickname = profile?.Nickname ?? $"Player-{userID[..5]}",
            Avatar = profile?.Avatar ?? string.Empty,
            IsReady = ReadyStatus.GetValueOrDefault(userID, false),
            IsOwner = userID == HostId
          });
        }
      }

      return roomData;
    }
  }

  public List<RoomDataPlayer> GetPlayerSnapshots()
  {
    lock (_lock)
    {
      ICollection<string> keys = Profiles.Keys.Any()
        ? Profiles.Keys
        : ReadyStatus.Keys;

      return keys.Select(userId =>
      {
        _ = Profiles.TryGetValue(userId, out UserProfile? profile);
        return new RoomDataPlayer
        {
          UserId = userId,
          Nickname = profile?.Nickname ?? $"Player-{userId[..5]}",
          Avatar = profile?.Avatar ?? string.Empty,
          IsReady = ReadyStatus.GetValueOrDefault(userId, false),
          IsOwner = userId == HostId
        };
      }).ToList();
    }
  }

  public List<WebSocket> GetParticipantsSnapshot()
  {
    lock (_lock)
    {
      return Participants.ToList();
    }
  }

  public void AddUser(string userID, UserProfile? profile)
  {
    lock (_lock)
    {
      if (!Profiles.TryGetValue(userID, out _))
      {
        _ = Profiles.TryAdd(userID, profile);
      }

      if (!ReadyStatus.TryGetValue(userID, out _))
      {
        _ = ReadyStatus.TryAdd(userID, false);
      }
    }
  }

  public void RemoveUser(string userID)
  {
    lock (_lock)
    {
      _ = Profiles.TryRemove(userID, out _);
      _ = ReadyStatus.TryRemove(userID, out _);
    }
  }

  public void UpdateSettings(GameSettings settings)
  {
    if (settings == null)
    {
      throw new ArgumentNullException(nameof(settings));
    }

    lock (_lock)
    {
      Settings = new GameSettings
      {
        TimeLimit = settings.TimeLimit,
        Order = settings.Order,
        Letters = settings.Letters != null ? settings.Letters.ToArray() : Array.Empty<string>()
      };
    }
  }
}

[Serializable]
public class UserProfile
{
  public string Nickname { get; set; } = "Player";

  public string Avatar { get; set; } = string.Empty;

  public string Email { get; set; } = string.Empty;
}

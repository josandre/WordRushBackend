using System.Collections.Concurrent;
using System.Net.WebSockets;
using WordRush.Core.Features.Realtime.Models;
using WordRush.Core.Features.Realtime.Models.GameSession;
using WordRush.Repository.Models;

public class GameRoom
{
  public string RoomId { get; }

  public string HostPlayerID { get; set; } = string.Empty;

  public bool Active { get; set; } = true;

  public bool InGame { get; set; } = false;

  public ConcurrentDictionary<string, UserProfile> Players { get; } = new();

  public ConcurrentDictionary<string, bool> PlayersReadyStatus { get; } = new();

  public GameSession Session { get; set; } = new();

  public GameSettings Settings { get; set; } = new();

  public List<WebSocket> PlayerSockets { get; } = new();

  private readonly object _lock = new();

  public GameRoom(string roomId)
  {
    RoomId = roomId;
  }

  public void ToggleReadyStateForPlayer(string userId)
  {
    lock (_lock)
    {
      PlayersReadyStatus[userId] = !PlayersReadyStatus.GetValueOrDefault(userId, false);
    }
  }

  public RoomDataRequestedEvent GetRoomData()
  {
    lock (_lock)
    {
      GameSettings currentSettings = Settings;

      RoomDataRequestedEvent roomData = new()
      {
        Settings = new GameSettings
        {
          TimeLimit = currentSettings.TimeLimit,
          Order = currentSettings.Order,
          Letters = currentSettings.Letters != null ? currentSettings.Letters : Array.Empty<string>()
        }
      };

      IEnumerator<string> userIDs = Players.Keys.GetEnumerator();
      while (userIDs.MoveNext())
      {
        string userID = userIDs.Current;

        if (Players.TryGetValue(userID, out UserProfile profile))
        {
          roomData.Players.Add(new RoomDataPlayer
          {
            UserId = userID,
            Nickname = profile?.Nickname ?? $"Player-{userID[..5]}",
            Avatar = profile?.Avatar ?? string.Empty,
            IsReady = PlayersReadyStatus.GetValueOrDefault(userID, false),
            IsOwner = userID == HostPlayerID
          });
        }
      }

      return roomData;
    }
  }

  public void AddPlayer(string userID, UserProfile? profile, WebSocket socket)
  {
    lock (_lock)
    {
      if (!Players.TryGetValue(userID, out _))
      {
        _ = Players.TryAdd(userID, profile);
      }

      if (!PlayersReadyStatus.TryGetValue(userID, out _))
      {
        _ = PlayersReadyStatus.TryAdd(userID, false);
      }

      if (!PlayerSockets.Contains(socket))
      {
        PlayerSockets.Add(socket);
      }
    }
  }

  public void RemovePlayer(string userID, WebSocket socket)
  {
    lock (_lock)
    {
      _ = Players.TryRemove(userID, out _);
      _ = PlayersReadyStatus.TryRemove(userID, out _);
      _ = PlayerSockets.Remove(socket);
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
      Settings = settings;
    }
  }

  public void PrepareGameSession()
  {
    lock (_lock)
    {
      Session = new();

      // TODO: Actually get the letters and timer based on the game settings
      Session.Setup(new string[] { "A", "B", "C" });
    }
  }

  /// <summary>
  /// Called when a player is ready to start the next session phase.
  /// </summary>
  /// <param name="userID">The User that is ready for the next phase.</param>
  /// <returns>If the session started the next phase or not.</returns>
  public bool OnPlayerReadyForNextPhase(string userID)
  {
    lock (_lock)
    {
      Session.OnPlayerReadyForNextRound(userID);
      Console.WriteLine($"Players ready for round: {Session.GetNumberOfPlayersReadyForNextRound()}, Total players: {Players.Count}");
      if (Session.GetNumberOfPlayersReadyForNextRound() >= Players.Count)
      {
        SessionState sessionState = Session.GetSessionState();
        Console.WriteLine("--- Session state: " + sessionState.ToString());
        if (sessionState is SessionState.WaitingPlayersToJoin or SessionState.InRoundResults)
        {
          Console.WriteLine("--- Starting new round");
          Session.StartNewRound();
        }

        return true;
      }

      return false;
    }
  }

  public SessionState GetSessionState()
  {
    lock (_lock)
    {
      return Session.GetSessionState();
    }
  }

  public void OnPlayerStop()
  {
    lock (_lock)
    {
      Session.OnStop();
    }
  }
}

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

  /// <summary>
  /// Tracks how many hint tokens each player has remaining.  
  /// The key is the user ID and the value is the number of tokens left.
  /// This dictionary is initialized when players join the room and whenever
  /// game settings (including the starting token count) are updated.
  /// </summary>
  public ConcurrentDictionary<string, int> PlayerHintTokens { get; } = new();

  public List<WebSocket> PlayerSockets { get; } = new();

  private readonly object _lock = new();

  public GameRoom(string roomId)
  {
    RoomId = roomId;
  }

  /// <summary>
  /// Returns the number of remaining hint tokens for the specified player.  
  /// If the player does not have an entry yet, returns the configured default
  /// from <see cref="Settings.HintTokens"/>.
  /// </summary>
  public int GetRemainingHints(string userId)
  {
    return PlayerHintTokens.TryGetValue(userId, out int tokens)
        ? tokens
        : Settings?.HintTokens ?? 0;
  }

  /// <summary>
  /// Consumes a single hint token for the specified player if available.  
  /// Returns <c>true</c> if a token was consumed, otherwise <c>false</c>.
  /// </summary>
  public bool UseHint(string userId)
  {
    lock (_lock)
    {
      int current = GetRemainingHints(userId);
      if (current <= 0)
      {
        return false;
      }

      PlayerHintTokens[userId] = current - 1;
      return true;
    }
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
        Settings = currentSettings
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

      // Initialise hint tokens for this player if not already present
      if (!PlayerHintTokens.ContainsKey(userID))
      {
        // Use the configured starting number of tokens from the current settings
        int startingTokens = Settings?.HintTokens ?? 0;
        _ = PlayerHintTokens.TryAdd(userID, startingTokens);
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

      // Remove the player's hint token entry
      _ = PlayerHintTokens.TryRemove(userID, out _);
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

      // Reset or update hint tokens for all players when settings change
      // If the configured number of tokens has changed, update each player's remaining tokens
      foreach (var userId in Players.Keys)
      {
        PlayerHintTokens[userId] = settings.HintTokens;
      }
    }
  }

  public void PrepareGameSession()
  {
    lock (_lock)
    {
      Session = new();
      Session.Setup(Settings.LettersArray);
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

  internal bool RegisterPlayerRoundResult(string userID, GameRoundResult result)
  {
    lock (_lock)
    {
      int numberOfResult = Session.RegisterUserRoundAnswers(userID, result);
      if (numberOfResult >= Players.Count)
      {
        Session.ChangeState(SessionState.InRoundResults);
        return true;
      }

      return false;
    }
  }

  internal int GetRoundIndex()
  {
    lock (_lock)
    {
      return Session.GetRoundIndex();
    }
  }
}

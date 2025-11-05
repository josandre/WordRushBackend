namespace WordRush.Core.Features.Realtime.Models.GameSession
{
  /// <summary>
  /// Represents a single game hosted in the server
  /// It handles the rounds creation and qualification.
  /// </summary>
  [Serializable]
  public class GameSession
  {
    // Flag to determine if the system is waiting for player confirmations for the next phase
    private bool waitingToStartNextRound = new();

    // For going to the next state is important that all players have confirmed that they are ready, this number will be set to 0.
    // With every confirmation it will increment by 1.
    private HashSet<string> playersReadyForNextRound = new();

    // Results for all the rounds (History for the final game results report)
    private List<GameRound> rounds = new();

    // This is where the magic happens, the active round that the callbacks will alter
    private GameRound activeRound;

    private string[] roundLetters;

    private SessionState currentState = new();

    private readonly object _lock = new();

    public void OnStop()
    {
      lock (_lock)
      {
        ChangeState(SessionState.WaitingForPlayerAnswers);
      }
    }

    public int RegisterUserRoundAnswers(string userID, GameRoundResult result)
    {
      lock (_lock)
      {
        activeRound.RegisterResult(userID, result);
        return activeRound.GetNumberOfResults();
      }
    }

    public void StartNewRound()
    {
      lock (_lock)
      {
        // First round
        if (currentState == SessionState.WaitingPlayersToJoin)
        {
          activeRound = new(roundLetters[0]);
        }
        else if (currentState == SessionState.InRoundResults)
        {
          rounds.Add(activeRound);
          activeRound = new(roundLetters[rounds.Count]);
        }
      }
    }

    public void UpdateRoundTimer()
    {
      // TODO:
    }

    public void ChangeState(SessionState newState)
    {
      lock (_lock)
      {
        currentState = newState;
        playersReadyForNextRound.Clear();
        waitingToStartNextRound = false;

        if (currentState is SessionState.WaitingPlayersToJoin or SessionState.InRoundResults)
        {
          waitingToStartNextRound = true;
        }
      }
    }

    public void OnPlayerReadyForNextRound(string userID)
    {
      lock (_lock)
      {
        if (!waitingToStartNextRound)
        {
          return;
        }

        if (!playersReadyForNextRound.Contains(userID))
        {
          _ = playersReadyForNextRound.Add(userID);
        }
      }
    }

    public int GetNumberOfPlayersReadyForNextRound()
    {
      lock (_lock)
      {
        return playersReadyForNextRound.Count;
      }
    }

    internal SessionState GetSessionState()
    {
      lock (_lock)
      {
        return currentState;
      }
    }

    internal void Setup(string[] letters)
    {
      roundLetters = letters;
      ChangeState(SessionState.WaitingPlayersToJoin);
    }
  }

  /// <summary>
  /// Defines the current state of the game session.
  /// </summary>
  public enum SessionState
  {
    WaitingPlayersToJoin, // The initial state when starting the game
    InRound,  // Where all the players are writing their answers
    WaitingForPlayerAnswers,  // When a player notified a Stop, the system should wait for all the player answers, so they can be evaluated later
    EvaluatingRoundResults, // When the system is ready to start evaluating and runs the logic to determine the score for every player
    InRoundResults, // When all the players are reviewing their answers and scores
    InGameResults // When the game finishes and the game report is sent to all the players
  }
}

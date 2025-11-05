
namespace WordRush.Core.Features.Realtime.Models.GameSession
{
  [Serializable]
  public class GameRound
  {
    public string Letter { get; set; } = string.Empty; // The letter played in this round
    public List<GameRoundResult> Results { get; set; } // List of the results of every player in the round

    private HashSet<string> userResults = new();  // Used to prevent duplicates in the game results
    private object _lock = new();

    public GameRound(string letter)
    {
      Letter = letter;
      Results = new();
    }

    internal void RegisterResult(string userID, GameRoundResult result)
    {
      lock (_lock)
      {
        if (!userResults.Contains(userID))
        {
          _ = userResults.Add(userID);
          Results.Add(result);
        }
      }
    }

    internal int GetNumberOfResults()
    {
      lock (_lock)
      {
        return Results.Count;
      }
    }
  }
}

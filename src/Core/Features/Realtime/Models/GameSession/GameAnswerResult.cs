namespace WordRush.Core.Features.Realtime.Models.GameSession
{
  /// <summary>
  /// Use to store the results of a specific answer.
  /// </summary>
  [Serializable]
  public class GameAnswerResult
  {
    public bool IsValid { get; set; } // Some answers might not be valid based on different conditions

    public int Score { get; set; } // The score obtained with the answer based in other players results
  }
}

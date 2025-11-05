namespace WordRush.Core.Features.Realtime.Models.GameSession
{
  [Serializable]
  public class GameRoundResult
  {
    public UserProfile User { get; set; } = new(); // Reference to the author of this result

    public List<GameAnswer> Answers { get; set; } = new(); // List of all the answers in this round
  }
}

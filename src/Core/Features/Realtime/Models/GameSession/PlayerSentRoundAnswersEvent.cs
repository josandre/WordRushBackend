namespace WordRush.Core.Features.Realtime.Models.GameSession
{
  /// <summary>
  /// Used to deserialize the JSON from the PlayerSentRoundAnswers event
  /// </summary>
  [Serializable]
  public class PlayerSentRoundAnswersEvent
  {
    public List<string> Answers { get; set; } = new();
  }
}

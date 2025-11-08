namespace WordRush.Core.Features.Realtime.Models.GameSession
{
  /// <summary>
  /// Used to share information about the round that will be started
  /// </summary>
  [Serializable]
  public class RoundStartedEvent
  {
    public string RoundLetter { get; set; }
  }
}

namespace WordRush.Core.Features.Realtime.Models
{
  /// <summary>
  /// Represents a hint request sent from the client via WebSocket.  
  /// Contains the game category and letter for which the player is requesting a hint.
  /// </summary>
  public class HintRequest
  {
    public string Category { get; set; } = string.Empty;
    public string Letter { get; set; } = string.Empty;
  }
}
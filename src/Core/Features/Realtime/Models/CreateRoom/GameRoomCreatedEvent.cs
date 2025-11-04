namespace WordRush.Core.Features.Realtime.Models.CreateRoom
{
  [Serializable]
  public class GameRoomCreatedEvent(string gameRoomID)
  {
    public string GameRoomID { get; set; } = gameRoomID;
    
    public GameSettings Settings { get; set; } = new();
  }
}

using WordRush.Repository.Models;

namespace WordRush.Core.Features.Realtime.Models.JoinRoom
{
  [Serializable]
  public class GameRoomJoinedEvent
  {
    public string GameRoomID { get; set; } = string.Empty;
    
    public GameSettings Settings { get; set; } = new();
    
    public CategoryType? CategoryType { get; set; }
  }
}

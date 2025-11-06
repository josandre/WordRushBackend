namespace WordRush.Core.Features.Realtime.Models
{
  [Serializable]
  public class RoomDataRequestedEvent
  {
    public List<RoomDataPlayer> Players { get; set; } = new();
    
    public GameSettings Settings { get; set; } = new();
  }

  [Serializable]
  public class RoomDataPlayer
  {
    public string UserId { get; set; } = string.Empty;

    public string Nickname { get; set; } = "Player";

    public string Avatar { get; set; } = string.Empty;

    public bool IsReady { get; set; }

    public bool IsOwner { get; set; }
  }
}

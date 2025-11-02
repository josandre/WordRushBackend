namespace WordRush.Web.Features.Game.Models;

public class UpdateGameSettingsRequest
{
  public string RoomId { get; set; } = string.Empty;

  public GameSettings Settings { get; set; } = new();
}


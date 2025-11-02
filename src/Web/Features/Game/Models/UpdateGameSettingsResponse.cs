namespace WordRush.Web.Features.Game.Models;

public class UpdateGameSettingsResponse
{
  public string RoomId { get; set; } = string.Empty;

  public GameSettings Settings { get; set; } = new();
}


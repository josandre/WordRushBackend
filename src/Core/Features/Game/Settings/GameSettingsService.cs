namespace WordRush.Core.Features.Game;

public class GameSettingsService : IGameSettingsService
{
  private readonly IWordRushWebSocketService _webSocketService;

  public GameSettingsService(IWordRushWebSocketService webSocketService)
  {
    _webSocketService = webSocketService;
  }

  public GameRoom? UpdateGameSettings(string roomId, GameSettings settings)
  {
    if (string.IsNullOrWhiteSpace(roomId) || settings == null)
    {
      return null;
    }

    GameRoom? room = _webSocketService.GetRoom(roomId);
    if (room != null)
    {
      room.UpdateSettings(settings);
      return room;
    }

    return null;
  }
}


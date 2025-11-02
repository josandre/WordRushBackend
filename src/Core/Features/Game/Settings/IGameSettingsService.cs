namespace WordRush.Core.Features.Game;

public interface IGameSettingsService
{
  GameRoom? UpdateGameSettings(string roomId, GameSettings settings);
}


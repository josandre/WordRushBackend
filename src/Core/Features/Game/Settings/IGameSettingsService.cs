namespace WordRush.Core.Features.Game;

public interface IGameSettingsService
{
  Task<GameRoom?> UpdateGameSettings(string roomId, GameSettings settings);
}


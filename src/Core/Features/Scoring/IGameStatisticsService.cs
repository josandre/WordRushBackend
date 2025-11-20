using WordRush.Repository.Models;

namespace WordRush.Core.Features.Scoring;

public interface IGameStatisticsService
{
  Task UpdateGameStatisticsAsync(int userId, bool won, int totalScore);
  Task<GameStatistics?> GetGameStatisticsByUserIdAsync(int userId);
}


using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WordRush.Repository;
using WordRush.Repository.Models;

namespace WordRush.Core.Features.Scoring;

public class GameStatisticsService : IGameStatisticsService
{
  private readonly AppDbContext _context;
  private readonly ILogger<GameStatisticsService> _logger;

  public GameStatisticsService(AppDbContext context, ILogger<GameStatisticsService> logger)
  {
    _context = context;
    _logger = logger;
  }

  public async Task UpdateGameStatisticsAsync(int userId, bool won, int totalScore)
  {
    try
    {
      GameStatistics? stats = await _context.GameStatistics
        .FirstOrDefaultAsync(gs => gs.UserId == userId);

      if (stats == null)
      {
        // Create new statistics record if it doesn't exist
        stats = new GameStatistics
        {
          UserId = userId,
          TotalPlayedGame = 0,
          WonGames = 0,
          TotalStore = 0
        };
        _context.GameStatistics.Add(stats);
      }

      // Update statistics
      stats.TotalPlayedGame += 1; // Increment games played
      if (won)
      {
        stats.WonGames += 1; // Increment won games only for winner
      }
      stats.TotalStore += totalScore; // Add the player's total score from this game

      await _context.SaveChangesAsync();

      _logger.LogInformation(
        "Updated game statistics for user {UserId}: TotalPlayed={TotalPlayed}, WonGames={WonGames}, TotalStore={TotalStore}",
        userId, stats.TotalPlayedGame, stats.WonGames, stats.TotalStore);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error updating game statistics for user {UserId}", userId);
      throw;
    }
  }

  public async Task<GameStatistics?> GetGameStatisticsByUserIdAsync(int userId)
  {
    try
    {
      GameStatistics? stats = await _context.GameStatistics
        .FirstOrDefaultAsync(gs => gs.UserId == userId);

      if (stats == null)
      {
        _logger.LogInformation("No game statistics found for user {UserId}", userId);
        return null;
      }

      _logger.LogInformation(
        "Retrieved game statistics for user {UserId}: TotalPlayed={TotalPlayed}, WonGames={WonGames}, TotalStore={TotalStore}",
        userId, stats.TotalPlayedGame, stats.WonGames, stats.TotalStore);

      return stats;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error retrieving game statistics for user {UserId}", userId);
      throw;
    }
  }
}


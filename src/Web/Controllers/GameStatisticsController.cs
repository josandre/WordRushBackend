using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WordRush.Core.Features.Scoring;
using WordRush.Web.Models;

namespace WordRush.Web.Controllers
{
  [Authorize]
  [Route("api/gamestatistics")]
  public class GameStatisticsController(IGameStatisticsService pGameStatisticsService) : ApiControllerBase
  {
    private readonly IGameStatisticsService gameStatisticsService = pGameStatisticsService;

    /// <summary>
    /// Gets the game statistics for a specific user by user ID.
    /// </summary>
    /// <param name="userId">The user ID to get statistics for.</param>
    /// <returns>The game statistics for the specified user.</returns>
    [HttpGet("{userId}")]
    public async Task<IActionResult> GetStatisticsByUserId(int userId)
    {
      var statistics = await gameStatisticsService.GetGameStatisticsByUserIdAsync(userId);

      return statistics == null
        ? NotFound($"No game statistics found for user {userId}.")
        : (IActionResult)Ok(new GameStatisticsResponse
        {
          UserId = statistics.UserId,
          TotalPlayedGame = statistics.TotalPlayedGame,
          WonGames = statistics.WonGames,
          TotalStore = statistics.TotalStore
        });
    }
  }
}


using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WordRush.Core.Features.StopGame;
using WordRush.Web.Controllers;

namespace WordRush.Web.Features.StopGame
{
  [Authorize]
  [Route("api/scoregame")]
  public class ScoreGameController : ApiControllerBase
  {
    private readonly IScoringService _scoringService;

    public ScoreGameController(IScoringService scoringService)
    {
      _scoringService = scoringService;
    }

    /// <summary>
    /// Scores a round of the Stop game using the AI-based scoring service.
    /// </summary>
    /// <param name="request">The round data including letter, categories, and player answers.</param>
    /// <returns>The scored results per player and category.</returns>
    [HttpPost("score")]
    public async Task<IActionResult> Score([FromBody] StopGameRequest request)
    {
      if (request == null)
        return BadRequest("Invalid request payload.");

      var result = await _scoringService.ScoreGameAsync(request);

      if (result == null)
        return StatusCode(500, "Scoring service returned no result.");

      return Ok(result);
    }
  }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WordRush.Core.Features.Scoring;
using WordRush.Core.Features.Scoring.Models;

namespace WordRush.Web.Controllers
{
  [Authorize]
  [Route("api/scoregame")]
  public class ScoreGameController(IScoringService pScoringService) : ApiControllerBase
  {
    private readonly IScoringService scoringService = pScoringService;

    /// <summary>
    /// Scores a round of the Stop game using the AI-based scoring service.
    /// </summary>
    /// <param name="request">The round data including letter, categories, and player answers.</param>
    /// <returns>The scored results per player and category.</returns>
    [HttpPost("score")]
    public async Task<IActionResult> Score([FromBody] StopGameRequest request)
    {
      if (!ModelState.IsValid)
      {
        return BadRequest(ModelState);
      }

      if (request == null)
      {
        return BadRequest("Invalid request payload.");
      }

      StopGameResponse? result = await scoringService.ScoreGameAsync(request);

      return result == null ? StatusCode(500, "Scoring service returned no result.") : (IActionResult)Ok(result);
    }
  }
}

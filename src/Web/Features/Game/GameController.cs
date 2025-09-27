using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using WordRush.Web.Features.Game.Data;

namespace WordRush.Web.Features.Game
{
  [EnableCors]
  [ApiController]
  [Route("api/games")]
  public class GameController : ControllerBase
  {
    /// <summary>
    /// Returns a list with all games of the user.
    /// </summary>
    /// <returns>200 OK with a all games for the user.</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<GameResponse>))]
    public Task<ActionResult<List<GameResponse>>> Get()
    {
      List<GameResponse> result =
        [
          new() { Id = 1, Name = "Dummy game 1" },
        new() { Id = 1, Name = "Dummy game 2" }
        ];

      Task<ActionResult<List<GameResponse>>> taskResult = Task.FromResult<ActionResult<List<GameResponse>>>(Ok(result));

      Log.Information("Game Dummy Result => {@taskResult}", taskResult);

      return taskResult;
    }
  }
}

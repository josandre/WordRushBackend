using Microsoft.AspNetCore.Mvc;

namespace WordRush.Web.Features.Game;

[ApiController]
[Route("api/games")]
public class GameController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok();
    }
}
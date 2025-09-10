using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace WordRush.Web.Features.Game;
[EnableCors]
[ApiController]
[Route("api/games")]
public class GameController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new { message = "Hello Brain-Hub" });
    }
}
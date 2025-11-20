using Microsoft.AspNetCore.Mvc;
using WordRush.Core.Features.Hints;

namespace WordRush.Web.Features.Game
{
  [ApiController]
  [Route("api/[controller]")]
  public class HintController : ControllerBase
  {
    private readonly IHintService _hintService;

    public HintController(IHintService hintService)
    {
      _hintService = hintService;
    }

    /// <summary>
    /// Test endpoint to manually request a hint for a given letter and category.
    /// </summary>
    /// <param name="letter">The starting letter (e.g. "C").</param>
    /// <param name="category">The category name (e.g. "Animal").</param>
    [HttpGet("get-hint")]
    public async Task<IActionResult> GetHint([FromQuery] string letter, [FromQuery] string category)
    {
      if (string.IsNullOrWhiteSpace(letter) || string.IsNullOrWhiteSpace(category))
      {
        return BadRequest("Both letter and category are required.");
      }

      try
      {
        var hint = await _hintService.GetHintAsync(letter, category);
        return Ok(new
        {
          letter,
          category,
          hint
        });
      }
      catch (Exception ex)
      {
        return StatusCode(500, new { error = ex.Message });
      }
    }
  }
}

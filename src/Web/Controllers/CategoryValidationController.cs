using Microsoft.AspNetCore.Mvc;
using WordRush.Core.Features.Settings;

namespace WordRush.Web.Controllers
{
  [ApiController]
  [Route("api/[controller]")]
  public class CategoryValidationController : ControllerBase
  {
    private readonly ICategoryValidationService _categoryValidationService;

    public CategoryValidationController(ICategoryValidationService categoryValidationService)
    {
      _categoryValidationService = categoryValidationService;
    }

    /// <summary>
    /// Test endpoint to manually request if a category is valid or not.
    /// </summary>
    /// <param name="category">The category.</param>
    /// <returns>True or false.</returns>
    [HttpGet("get-hint")]
    public async Task<IActionResult> CategoryValidationCheck([FromQuery] string category)
    {
      if (string.IsNullOrWhiteSpace(category))
      {
        return BadRequest("The category is required");
      }

      try
      {
        var isValid = await _categoryValidationService.GetCategoryValidationAsync(category);
        return Ok(new
        {
          category,
          isValid
        });
      }
      catch (Exception ex)
      {
        return StatusCode(500, new { error = ex.Message });
      }
    }
  }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WordRush.Core.Features.Game.CategoryColumns;
using WordRush.Web.Controllers;
using WordRush.Web.Features.Game.CategoryColumns.Models;

namespace WordRush.Web.Features.Game.CategoryColumns;

[Authorize]
[Route("api/category-columns")]
[ApiController]
public class CategoryColumnsController : ApiControllerBase
{
  private readonly ICategoryColumns _categoryColumnsService;

  public CategoryColumnsController(ICategoryColumns categoryColumnsService)
  {
    _categoryColumnsService = categoryColumnsService;
  }

  [HttpGet("default")]
  public async Task<ActionResult<CategoryColumnsResponse>> GetDefaultCategories()
  {
    var categoryType = await _categoryColumnsService.GetDefaultCategories();

    if (categoryType == null)
    {
      return NotFound("No default category type found");
    }

    var response = new CategoryColumnsResponse
    {
      CategoryType = new CategoryTypeDto
      {
        Id = categoryType.Id,
        Name = categoryType.Name,
        CategoryColumns = categoryType.CategoryColumns.Select(c => new CategoryColumnDto
        {
          Id = c.Id,
          Column = c.Column
        }).ToList()
      }
    };

    return Ok(response);
  }
}

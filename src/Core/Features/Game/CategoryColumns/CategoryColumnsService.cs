using Microsoft.EntityFrameworkCore;
using WordRush.Core.Features.Game.C;
using WordRush.Core.Features.Game.CategoryTypes;
using WordRush.Repository;
using WordRush.Repository.Models;

namespace WordRush.Core.Features.Game.

public class GameCategoryService : IGameCategories
{
  private readonly AppDbContext _dbContext;
  private readonly CategoryTypesService _categoryTypesService;

  public GameCategoryService(AppDbContext dbContext, CategoryTypesService categoryTypesService)
  {
    _dbContext = dbContext;
    _categoryTypesService = categoryTypesService;
  }

  public async Task<List<CategoryColumn>> GetDefaultCategories()
  {
   var defaultType = _categoryTypesService.GetDefaultType()
   if (defaultType != null)
   {
     return await _dbContext.CategoryColumns
       .Where(c => c.CategoryType.Id == defaultType.Id)
       .ToListAsync();
   }

   return null;
  }
}

using Microsoft.EntityFrameworkCore;
using WordRush.Core.Features.Game.CategoryTypes;
using WordRush.Repository;
using WordRush.Repository.Models;

namespace WordRush.Core.Features.Game.CategoryColumns;

public class CategoryColumnsService : ICategoryColumns
{
  private readonly AppDbContext _dbContext;
  private readonly ICategoryTypes _categoryTypesService;

  public CategoryColumnsService(AppDbContext dbContext, ICategoryTypes categoryTypesService)
  {
    _dbContext = dbContext;
    _categoryTypesService = categoryTypesService;
  }

  public async Task<CategoryType?> GetDefaultCategories()
  {
    var defaultType = await _categoryTypesService.GetDefaultType();

    if (defaultType == null)
    {
      return null;
    }

    return await _dbContext.CategoryTypes
      .Include(ct => ct.CategoryColumns)
      .FirstOrDefaultAsync(ct => ct.Id == defaultType.Id);
  }
}

using Microsoft.EntityFrameworkCore;
using WordRush.Repository;
using WordRush.Repository.Models;

namespace WordRush.Core.Features.Game.CategoryTypes;

public class CategoryTypesService : ICategoryTypes
{
  private readonly AppDbContext _dbContext;

  public CategoryTypesService(AppDbContext dbContext)
  {
    _dbContext = dbContext;
  }

  public async Task<CategoryType> GetDefaultType()
  {
    var defaultType = await _dbContext.CategoryTypes
      .FirstOrDefaultAsync(type => type.Name == "Default");

    return defaultType;
  }
}

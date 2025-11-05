using WordRush.Repository.Models;

namespace WordRush.Core.Features.Game.CategoryTypes;

public interface ICategoryTypes
{
  public Task<CategoryType?> GetDefaultType();
  
  public Task<CategoryType?> GetDefaultTypeWithColumns();
}

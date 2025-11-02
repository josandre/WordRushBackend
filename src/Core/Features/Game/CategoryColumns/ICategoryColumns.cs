using WordRush.Repository.Models;

namespace WordRush.Core.Features.Game.CategoryColumns;

public interface ICategoryColumns
{
  public Task<CategoryType?> GetDefaultCategories();
}

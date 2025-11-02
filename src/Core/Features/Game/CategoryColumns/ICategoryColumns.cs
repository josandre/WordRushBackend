using WordRush.Repository.Models;

namespace WordRush.Core.Features.Game.Ca;

public interface IGameCategories
{
  public Task<List<CategoryColumn>> GetDefaultCategories();
}

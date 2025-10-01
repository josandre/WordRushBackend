using WordRush.Repository;
using WordRush.Repository.Models;

namespace WordRush.Core.Features;

public class RoleService: IRoleService
{
  private readonly AppDbContext _dbContext;

  public RoleService(AppDbContext dbContext)
  {
    _dbContext = dbContext;
  }

  public async Task<Role> GetRoleById(int id)
  {
    return await _dbContext.Roles.FindAsync(id);
  }
}

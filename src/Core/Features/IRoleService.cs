using WordRush.Repository.Models;

namespace WordRush.Core.Features;

public interface IRoleService
{
  Task<Role> GetRoleById(int id);
}

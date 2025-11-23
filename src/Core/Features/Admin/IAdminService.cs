using System.Collections.Generic;
using System.Threading.Tasks;

namespace WordRush.Core.Features.Admin
{
  /// <summary>
  /// Contract for administrative operations over the user set.
  /// </summary>
  public interface IAdminService
  {
    /// <summary>
    /// Retrieves the list of users with optional search and sorting.
    /// </summary>
    Task<IReadOnlyList<AdminUserDto>> GetUsersAsync(string search, string sortBy, bool ascending);

    /// <summary>
    /// Toggles the active flag of a user.
    /// </summary>
    Task<bool> ToggleUserActiveAsync(int userId);

    Task<bool> SetUserRoleAsync(int userId, int roleId);

  }
}

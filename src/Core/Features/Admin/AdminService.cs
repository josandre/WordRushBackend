using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WordRush.Repository;
using WordRush.Repository.Models;

namespace WordRush.Core.Features.Admin
{
  /// <summary>
  /// Default implementation of administrative operations.
  /// </summary>
  public class AdminService : IAdminService
  {
    private readonly AppDbContext dbContext;

    public AdminService(AppDbContext dbContext)
    {
      this.dbContext = dbContext;
    }

    public async Task<IReadOnlyList<AdminUserDto>> GetUsersAsync(string search, string sortBy, bool ascending)
    {
      IQueryable<User> query = dbContext.Users
        .Include(u => u.GameStatistics)
        .AsNoTracking();

      if (!string.IsNullOrWhiteSpace(search))
      {
        string term = search.Trim();
        query = query.Where(u =>
          EF.Functions.ILike(u.Nickname, $"%{term}%") ||
          EF.Functions.ILike(u.Email, $"%{term}%"));
      }

      // Sorting
      query = (sortBy?.ToLowerInvariant()) switch
      {
        "nickname" => ascending ? query.OrderBy(u => u.Nickname) : query.OrderByDescending(u => u.Nickname),
        "email" => ascending ? query.OrderBy(u => u.Email) : query.OrderByDescending(u => u.Email),
        "createdon" => ascending ? query.OrderBy(u => u.CreatedOn) : query.OrderByDescending(u => u.CreatedOn),
        "totalplayedgame" => ascending
          ? query.OrderBy(u => u.GameStatistics != null ? u.GameStatistics.TotalPlayedGame : 0)
          : query.OrderByDescending(u => u.GameStatistics != null ? u.GameStatistics.TotalPlayedGame : 0),
        "wongames" => ascending
          ? query.OrderBy(u => u.GameStatistics != null ? u.GameStatistics.WonGames : 0)
          : query.OrderByDescending(u => u.GameStatistics != null ? u.GameStatistics.WonGames : 0),
        "totalstore" => ascending
          ? query.OrderBy(u => u.GameStatistics != null ? u.GameStatistics.TotalStore : 0)
          : query.OrderByDescending(u => u.GameStatistics != null ? u.GameStatistics.TotalStore : 0),
        _ => ascending ? query.OrderBy(u => u.Id) : query.OrderByDescending(u => u.Id),
      };

      List<User> users = await query.ToListAsync();

      return users.Select(u => new AdminUserDto
      {
        Id = u.Id,
        Nickname = u.Nickname,
        Email = u.Email,
        RoleId = u.RoleId,
        CreatedOn = u.CreatedOn,
        LastActivityDate = u.LastActivityDate,
        IsActive = u.IsActive,
        TotalPlayedGame = u.GameStatistics?.TotalPlayedGame ?? 0,
        WonGames = u.GameStatistics?.WonGames ?? 0,
        TotalStore = u.GameStatistics?.TotalStore ?? 0,
      }).ToList();
    }

    public async Task<bool> ToggleUserActiveAsync(int userId)
    {
      User user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);

      if (user == null)
      {
        return false;
      }

      user.IsActive = !user.IsActive;
      await dbContext.SaveChangesAsync();
      return true;
    }
  }
}

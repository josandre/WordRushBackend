using Microsoft.EntityFrameworkCore;
using WordRush.Repository;

namespace WordRush.Core.Features;

public class UserService : IUserService
{
  private readonly AppDbContext dbContext;

  public UserService(AppDbContext dbContext)
  {
    this.dbContext = dbContext;
  }

  public async Task<bool> GetUserByEmail(string email)
  {
    return await dbContext.Users.AnyAsync(user => user.Email == email);
  }
}

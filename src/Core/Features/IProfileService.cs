using WordRush.Repository.Models;

namespace WordRush.Core.Features
{
  public interface IProfileService
  {
    Task<User?> GetUserProfileByEmail(string email);
  }
}

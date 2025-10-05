using WordRush.Repository.Models;

namespace WordRush.Core.Features
{
  public interface IProfileService
  {
    Task<User?> GetUserProfileByEmail(string email);

    Task<User?> UpdateUserProfile(int id, string nickname, string avatar, string email, string password);
  }
}

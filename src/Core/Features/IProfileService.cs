using WordRush.Repository.Models;

namespace WordRush.Core.Features
{
  public interface IProfileService
  {
    Task<User?> GetUserProfileByEmail(string email);

    Task<User?> UpdateUserProfile(int id, string nickname, string avatar, string email);

    Task<bool> ChangeUserPassword(int userId, string currentPassword, string newPassword);
  }
}

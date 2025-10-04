using Microsoft.AspNetCore.Identity;
using WordRush.Repository.Models;

namespace WordRush.Core.Features
{
  public class ProfileService(SignInManager<User> signInManager) : IProfileService
  {
    private readonly SignInManager<User> _signInManager = signInManager;

    public async Task<User?> GetUserProfileByEmail(string email)
    {
      return await _signInManager.UserManager.FindByEmailAsync(email);
    }
  }
}

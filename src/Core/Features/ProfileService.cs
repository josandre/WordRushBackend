using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WordRush.Repository;
using WordRush.Repository.Models;

namespace WordRush.Core.Features
{
  public class ProfileService(SignInManager<User> signInManager, AppDbContext dbContext) : IProfileService, IPasswordHasher<User>
  {
    private readonly SignInManager<User> _signInManager = signInManager;
    private readonly AppDbContext _dbContext = dbContext;
    private readonly PasswordHasher<User> _passwordHasher = new();

    public async Task<User?> GetUserProfileByEmail(string email)
    {
      return await _signInManager.UserManager.FindByEmailAsync(email);
    }

    public async Task<User?> UpdateUserProfile(int id, string nickname, string avatar, string email, string password)
    {
      User? user = await _dbContext.FindAsync<User>(id);
      if (user != null)
      {
        user.Nickname = nickname;
        user.Avatar = avatar;
        user.Email = email;
        if (!string.IsNullOrWhiteSpace(password))
        {
          user.PasswordHash = HashPassword(user, password);
        }

        IdentityResult result = await _signInManager.UserManager.UpdateAsync(user);

        return result.Succeeded ? user : null;
      }

      return null;
    }

    public string HashPassword(User user, string password)
    {
      return _passwordHasher.HashPassword(user, password);
    }

    public PasswordVerificationResult VerifyHashedPassword(User user, string hashedPassword, string providedPassword)
    {
      return _passwordHasher.VerifyHashedPassword(user, hashedPassword, providedPassword);
    }
  }
}

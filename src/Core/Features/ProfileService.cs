//using Microsoft.AspNetCore.Identity;
//using Microsoft.EntityFrameworkCore;
//using WordRush.Repository;
//using WordRush.Repository.Models;

//namespace WordRush.Core.Features
//{
//  public class ProfileService(SignInManager<User> signInManager, AppDbContext dbContext) : IProfileService, IPasswordHasher<User>
//  {
//    private readonly PasswordHasher<User> passwordHasher = new();

//    public async Task<User?> GetUserProfileByEmail(string email)
//    {
//      return await signInManager.UserManager.FindByEmailAsync(email);
//    }

//    public async Task<User?> UpdateUserProfile(int id, string nickname, string avatar, string email)
//    {
//      User? user = await dbContext.FindAsync<User>(id);
//      if (user != null)
//      {
//        user.Nickname = nickname;
//        user.Avatar = avatar;
//        user.Email = email;

//        IdentityResult result = await signInManager.UserManager.UpdateAsync(user);

//        return result.Succeeded ? user : null;
//      }

//      return null;
//    }

//    public async Task<bool> ChangeUserPassword(int userId, string currentPassword, string newPassword)
//    {
//      User? user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
//      if (user == null)
//      {
//        return false;
//      }

//      // Verify current password
//      PasswordVerificationResult verificationResult = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, currentPassword);

//      if (verificationResult == PasswordVerificationResult.Failed)
//      {
//        return false;
//      }

//      // Generate new hash and save
//      user.PasswordHash = HashPassword(user, newPassword);

//      await dbContext.SaveChangesAsync();

//      return true;
//    }

//    public string HashPassword(User user, string password)
//    {
//      return passwordHasher.HashPassword(user, password);
//    }

//    public PasswordVerificationResult VerifyHashedPassword(User user, string hashedPassword, string providedPassword)
//    {
//      return passwordHasher.VerifyHashedPassword(user, hashedPassword, providedPassword);
//    }
//  }
//}

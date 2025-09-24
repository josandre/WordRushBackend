using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using WordRush.Core.Infrastructure.Identity.Models;
using WordRush.Repository.Models;

namespace WordRush.Core.Infrastructure.Identity;

public class AuthService : IAuthService
{
  private readonly UserManager<User> _userManager;
  private readonly string _issuer = "wordsRush";
  private readonly string _key = "wordsRush"; //TODO : Change to a user secret

  public AuthService(UserManager<User> userManager)
  {
    _userManager = userManager;
  }

  public async Task<LoginResponse> Login(User user)
  {
    var result = new LoginResponse
    {
      AccessToken = await this.GenerateTokenAsync(user),
    };

    return result;
  }

  private async Task<string> GenerateTokenAsync(User user)
  {
    var claims = new List<Claim>
    {
      new(JwtRegisteredClaimNames.Sub, user.UserName ?? ""),
      new(ClaimTypes.NameIdentifier, user.Id.ToString())
    };

    var roles = await _userManager.GetRolesAsync(user);
    claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_key));
    var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

    var token = new JwtSecurityToken(
      issuer: _issuer,
      audience: _issuer,
      claims: claims,
      expires: DateTime.UtcNow.AddHours(1),
      signingCredentials: creds
    );

    return new JwtSecurityTokenHandler().WriteToken(token);
  }
}

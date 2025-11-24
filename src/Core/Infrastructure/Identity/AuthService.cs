using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using WordRush.Core.Features;
using WordRush.Core.Infrastructure.Identity.Models;
using WordRush.Repository;
using WordRush.Repository.Models;

namespace WordRush.Core.Infrastructure.Identity;

public class AuthService : IAuthService
{
  private readonly IRoleService _roleService;
  private readonly UserManager<User> _userManager;
  private readonly string _issuer;
  private readonly string _key;

  public AuthService(UserManager<User> userManager, IRoleService roleService, IConfiguration config)
  {
    _userManager = userManager;
    _roleService = roleService;
    _issuer = config["Jwt:Issuer"];
    _key = config["Jwt:Secret"];
  }

  public async Task<LoginResponse> Login(User user)
  {
    LoginResponse result = new()
    {
      AccessToken = await GenerateTokenAsync(user),
    };

    return result;
  }

  private async Task<string> GenerateTokenAsync(User user)
  {
    List<Claim> claims =
    [
      new(JwtRegisteredClaimNames.Sub, user.UserName ?? string.Empty),
      new(ClaimTypes.NameIdentifier, user.Id.ToString()),
      new(ClaimTypes.Name, user.UserName ?? string.Empty)
    ];

    Role role = await _roleService.GetRoleById(user.RoleId);
    claims.Add(new Claim(ClaimTypes.Role, role.Name));

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

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Moq;
using WordRush.Core.Features;
using WordRush.Core.Infrastructure.Identity;
using WordRush.Repository.Models;

namespace WordRush.Core.Tests.Infrastructure.Identity;

public class AuthServiceTests
{
  private IConfiguration CreateConfiguration()
  {
    var configValues = new Dictionary<string, string>
    {
      { "Jwt:Issuer", "test-issuer" },
      { "Jwt:Secret", "test-secret-key-that-is-at-least-32-characters-long-for-hmac-sha256" }
    };

    return new ConfigurationBuilder()
      .AddInMemoryCollection(configValues)
      .Build();
  }

  private Mock<UserManager<User>> CreateUserManagerMock()
  {
    var userStoreMock = new Mock<IUserStore<User>>();
    return new Mock<UserManager<User>>(
      userStoreMock.Object, null, null, null, null, null, null, null, null);
  }

  [Fact]
  public async Task Login_ReturnsLoginResponse_WithAccessToken()
  {
    var userManagerMock = CreateUserManagerMock();
    var roleServiceMock = new Mock<IRoleService>();
    var config = CreateConfiguration();

    var user = new User
    {
      Id = 1,
      UserName = "testuser",
      Email = "test@example.com",
      RoleId = 1
    };

    var role = new Role
    {
      Id = 1,
      Name = "User"
    };

    roleServiceMock.Setup(x => x.GetRoleById(1))
      .ReturnsAsync(role);

    var service = new AuthService(userManagerMock.Object, roleServiceMock.Object, config);

    var result = await service.Login(user);

    Assert.NotNull(result);
    Assert.NotNull(result.AccessToken);
    Assert.NotEmpty(result.AccessToken);
  }

  [Fact]
  public async Task Login_GeneratesValidJwtToken()
  {
    var userManagerMock = CreateUserManagerMock();
    var roleServiceMock = new Mock<IRoleService>();
    var config = CreateConfiguration();

    var user = new User
    {
      Id = 1,
      UserName = "testuser",
      Email = "test@example.com",
      RoleId = 1
    };

    var role = new Role
    {
      Id = 1,
      Name = "Admin"
    };

    roleServiceMock.Setup(x => x.GetRoleById(1))
      .ReturnsAsync(role);

    var service = new AuthService(userManagerMock.Object, roleServiceMock.Object, config);

    var result = await service.Login(user);

    var tokenHandler = new JwtSecurityTokenHandler();
    var jsonToken = tokenHandler.ReadJwtToken(result.AccessToken);

    Assert.NotNull(jsonToken);
    Assert.Equal("test-issuer", jsonToken.Issuer);
    Assert.Equal("test-issuer", jsonToken.Audiences.First());
  }

  [Fact]
  public async Task Login_TokenContainsCorrectClaims()
  {
    var userManagerMock = CreateUserManagerMock();
    var roleServiceMock = new Mock<IRoleService>();
    var config = CreateConfiguration();

    var user = new User
    {
      Id = 1,
      UserName = "testuser",
      Email = "test@example.com",
      RoleId = 1
    };

    var role = new Role
    {
      Id = 1,
      Name = "User"
    };

    roleServiceMock.Setup(x => x.GetRoleById(1))
      .ReturnsAsync(role);

    var service = new AuthService(userManagerMock.Object, roleServiceMock.Object, config);

    var result = await service.Login(user);

    var tokenHandler = new JwtSecurityTokenHandler();
    var jsonToken = tokenHandler.ReadJwtToken(result.AccessToken);

    var nameIdentifierClaim = jsonToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
    var nameClaim = jsonToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
    var roleClaim = jsonToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role);
    var subClaim = jsonToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub);

    Assert.NotNull(nameIdentifierClaim);
    Assert.Equal("1", nameIdentifierClaim.Value);
    Assert.NotNull(nameClaim);
    Assert.Equal("testuser", nameClaim.Value);
    Assert.NotNull(roleClaim);
    Assert.Equal("User", roleClaim.Value);
    Assert.NotNull(subClaim);
    Assert.Equal("testuser", subClaim.Value);
  }

  [Fact]
  public async Task Login_TokenExpiresAfterOneHour()
  {
    var userManagerMock = CreateUserManagerMock();
    var roleServiceMock = new Mock<IRoleService>();
    var config = CreateConfiguration();

    var user = new User
    {
      Id = 1,
      UserName = "testuser",
      Email = "test@example.com",
      RoleId = 1
    };

    var role = new Role
    {
      Id = 1,
      Name = "User"
    };

    roleServiceMock.Setup(x => x.GetRoleById(1))
      .ReturnsAsync(role);

    var service = new AuthService(userManagerMock.Object, roleServiceMock.Object, config);

    var beforeGeneration = DateTime.UtcNow;
    var result = await service.Login(user);
    var afterGeneration = DateTime.UtcNow;

    var tokenHandler = new JwtSecurityTokenHandler();
    var jsonToken = tokenHandler.ReadJwtToken(result.AccessToken);

    Assert.True(jsonToken.ValidTo > beforeGeneration.AddHours(1).AddMinutes(-1));
    Assert.True(jsonToken.ValidTo < afterGeneration.AddHours(1).AddMinutes(1));
  }

  [Fact]
  public async Task Login_HandlesNullUserName()
  {
    var userManagerMock = CreateUserManagerMock();
    var roleServiceMock = new Mock<IRoleService>();
    var config = CreateConfiguration();

    var user = new User
    {
      Id = 1,
      UserName = null,
      Email = "test@example.com",
      RoleId = 1
    };

    var role = new Role
    {
      Id = 1,
      Name = "User"
    };

    roleServiceMock.Setup(x => x.GetRoleById(1))
      .ReturnsAsync(role);

    var service = new AuthService(userManagerMock.Object, roleServiceMock.Object, config);

    var result = await service.Login(user);

    Assert.NotNull(result);
    Assert.NotNull(result.AccessToken);

    var tokenHandler = new JwtSecurityTokenHandler();
    var jsonToken = tokenHandler.ReadJwtToken(result.AccessToken);

    var subClaim = jsonToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub);
    Assert.NotNull(subClaim);
    Assert.Equal(string.Empty, subClaim.Value);
  }

  [Fact]
  public async Task Login_IncludesRoleInToken()
  {
    var userManagerMock = CreateUserManagerMock();
    var roleServiceMock = new Mock<IRoleService>();
    var config = CreateConfiguration();

    var user = new User
    {
      Id = 1,
      UserName = "adminuser",
      Email = "admin@example.com",
      RoleId = 2
    };

    var role = new Role
    {
      Id = 2,
      Name = "Admin"
    };

    roleServiceMock.Setup(x => x.GetRoleById(2))
      .ReturnsAsync(role);

    var service = new AuthService(userManagerMock.Object, roleServiceMock.Object, config);

    var result = await service.Login(user);

    var tokenHandler = new JwtSecurityTokenHandler();
    var jsonToken = tokenHandler.ReadJwtToken(result.AccessToken);

    var roleClaim = jsonToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role);

    Assert.NotNull(roleClaim);
    Assert.Equal("Admin", roleClaim.Value);
  }
}


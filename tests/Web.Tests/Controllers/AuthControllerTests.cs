using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Moq;
using WordRush.Core.Features;
using WordRush.Core.Infrastructure.Identity;
using WordRush.Core.Infrastructure.Identity.Models;
using WordRush.Repository.Models;
using WordRush.Web.Controllers;
using WordRush.Web.Models;

namespace WordRush.Web.Tests.Controllers;

public class AuthControllerTests
{
  private (Mock<SignInManager<User>>, Mock<UserManager<User>>) CreateSignInManagerWithUserManagerMock()
  {
    var userStoreMock = new Mock<IUserStore<User>>();
    var userManagerMock = new Mock<UserManager<User>>(
      userStoreMock.Object, null, null, null, null, null, null, null, null);

    var signInManagerMock = new Mock<SignInManager<User>>(
      userManagerMock.Object,
      Mock.Of<Microsoft.AspNetCore.Http.IHttpContextAccessor>(),
      Mock.Of<IUserClaimsPrincipalFactory<User>>(),
      null, null, null, null);

    return (signInManagerMock, userManagerMock);
  }

  [Fact]
  public async Task SignIn_ReturnsOk_WhenCredentialsAreValid()
  {
    var (signInManager, userManagerMock) = CreateSignInManagerWithUserManagerMock();
    var authServiceMock = new Mock<IAuthService>();
    var separateUserManagerMock = new Mock<UserManager<User>>(
      Mock.Of<IUserStore<User>>(), null, null, null, null, null, null, null, null);
    var roleServiceMock = new Mock<IRoleService>();
    var userServiceMock = new Mock<IUserService>();

    var user = new User
    {
      Id = 1,
      Email = "test@example.com",
      UserName = "testuser",
      IsActive = true
    };

    var loginResponse = new LoginResponse
    {
      AccessToken = "test-token"
    };

    userManagerMock.Setup(x => x.FindByEmailAsync("test@example.com"))
      .ReturnsAsync(user);
    signInManager.Setup(x => x.CheckPasswordSignInAsync(user, "password", true))
      .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);
    authServiceMock.Setup(x => x.Login(user))
      .ReturnsAsync(loginResponse);

    var controller = new AuthController(
      signInManager.Object,
      authServiceMock.Object,
      separateUserManagerMock.Object,
      roleServiceMock.Object,
      userServiceMock.Object);

    var request = new LoginRequest
    {
      Email = "test@example.com",
      Password = "password"
    };

    var result = await controller.SignIn(request);

    var okResult = Assert.IsType<OkObjectResult>(result.Result);
    var response = Assert.IsType<LoginResponse>(okResult.Value);
    Assert.Equal("test-token", response.AccessToken);
  }

  [Fact]
  public async Task SignIn_ReturnsBadRequest_WhenUserDoesNotExist()
  {
    var (signInManager, userManagerMock) = CreateSignInManagerWithUserManagerMock();
    var authServiceMock = new Mock<IAuthService>();
    var separateUserManagerMock = new Mock<UserManager<User>>(
      Mock.Of<IUserStore<User>>(), null, null, null, null, null, null, null, null);
    var roleServiceMock = new Mock<IRoleService>();
    var userServiceMock = new Mock<IUserService>();

    userManagerMock.Setup(x => x.FindByEmailAsync("nonexistent@example.com"))
      .ReturnsAsync((User?)null);
    var controller = new AuthController(
      signInManager.Object,
      authServiceMock.Object,
      separateUserManagerMock.Object,
      roleServiceMock.Object,
      userServiceMock.Object);

    var request = new LoginRequest
    {
      Email = "nonexistent@example.com",
      Password = "password"
    };

    var result = await controller.SignIn(request);

    Assert.IsType<BadRequestObjectResult>(result.Result);
  }

  [Fact]
  public async Task SignIn_ReturnsBadRequest_WhenUserIsInactive()
  {
    var (signInManager, userManagerMock) = CreateSignInManagerWithUserManagerMock();
    var authServiceMock = new Mock<IAuthService>();
    var separateUserManagerMock = new Mock<UserManager<User>>(
      Mock.Of<IUserStore<User>>(), null, null, null, null, null, null, null, null);
    var roleServiceMock = new Mock<IRoleService>();
    var userServiceMock = new Mock<IUserService>();

    var user = new User
    {
      Id = 1,
      Email = "test@example.com",
      UserName = "testuser",
      IsActive = false
    };

    userManagerMock.Setup(x => x.FindByEmailAsync("test@example.com"))
      .ReturnsAsync(user);
    var controller = new AuthController(
      signInManager.Object,
      authServiceMock.Object,
      separateUserManagerMock.Object,
      roleServiceMock.Object,
      userServiceMock.Object);

    var request = new LoginRequest
    {
      Email = "test@example.com",
      Password = "password"
    };

    var result = await controller.SignIn(request);

    var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
    Assert.Contains("ACCOUNT_INACTIVE", badRequestResult.Value?.ToString() ?? string.Empty);
  }

  [Fact]
  public async Task SignIn_ReturnsBadRequest_WhenPasswordIsIncorrect()
  {
    var (signInManager, userManagerMock) = CreateSignInManagerWithUserManagerMock();
    var authServiceMock = new Mock<IAuthService>();
    var separateUserManagerMock = new Mock<UserManager<User>>(
      Mock.Of<IUserStore<User>>(), null, null, null, null, null, null, null, null);
    var roleServiceMock = new Mock<IRoleService>();
    var userServiceMock = new Mock<IUserService>();

    var user = new User
    {
      Id = 1,
      Email = "test@example.com",
      UserName = "testuser",
      IsActive = true
    };

    userManagerMock.Setup(x => x.FindByEmailAsync("test@example.com"))
      .ReturnsAsync(user);
    signInManager.Setup(x => x.CheckPasswordSignInAsync(user, "wrongpassword", true))
      .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Failed);

    var controller = new AuthController(
      signInManager.Object,
      authServiceMock.Object,
      separateUserManagerMock.Object,
      roleServiceMock.Object,
      userServiceMock.Object);

    var request = new LoginRequest
    {
      Email = "test@example.com",
      Password = "wrongpassword"
    };

    var result = await controller.SignIn(request);

    Assert.IsType<BadRequestObjectResult>(result.Result);
  }

  [Fact]
  public async Task SignUp_ReturnsBadRequest_WhenEmailAlreadyExists()
  {
    var (signInManagerMock, _) = CreateSignInManagerWithUserManagerMock();
    var authServiceMock = new Mock<IAuthService>();
    var userManagerMock = new Mock<UserManager<User>>(
      Mock.Of<IUserStore<User>>(), null, null, null, null, null, null, null, null);
    var roleServiceMock = new Mock<IRoleService>();
    var userServiceMock = new Mock<IUserService>();

    userServiceMock.Setup(x => x.GetUserByEmail("existing@example.com"))
      .ReturnsAsync(true);

    var controller = new AuthController(
      signInManagerMock.Object,
      authServiceMock.Object,
      userManagerMock.Object,
      roleServiceMock.Object,
      userServiceMock.Object);

    var request = new SignUpRequest
    {
      Email = "existing@example.com",
      UserName = "testuser",
      Password = "Password123!",
      Nickname = "Test",
      Avatar = "avatar.png"
    };

    var result = await controller.SignUp(request);

    Assert.IsType<BadRequestObjectResult>(result.Result);
  }

  [Fact]
  public async Task SignUp_ReturnsOk_WhenUserIsCreatedSuccessfully()
  {
    var (signInManagerMock, _) = CreateSignInManagerWithUserManagerMock();
    var authServiceMock = new Mock<IAuthService>();
    var userManagerMock = new Mock<UserManager<User>>(
      Mock.Of<IUserStore<User>>(), null, null, null, null, null, null, null, null);
    var roleServiceMock = new Mock<IRoleService>();
    var userServiceMock = new Mock<IUserService>();

    var role = new Role
    {
      Id = 1,
      Name = "User"
    };

    userServiceMock.Setup(x => x.GetUserByEmail("new@example.com"))
      .ReturnsAsync(false);
    roleServiceMock.Setup(x => x.GetRoleById(1))
      .ReturnsAsync(role);
    userManagerMock.Setup(x => x.CreateAsync(It.IsAny<User>(), It.IsAny<string>()))
      .ReturnsAsync(IdentityResult.Success)
      .Callback<User, string>((u, p) =>
      {
        u.Id = 1;
        u.RoleId = 1;
      });

    var controller = new AuthController(
      signInManagerMock.Object,
      authServiceMock.Object,
      userManagerMock.Object,
      roleServiceMock.Object,
      userServiceMock.Object);

    var request = new SignUpRequest
    {
      Email = "new@example.com",
      UserName = "testuser",
      Password = "Password123!",
      Nickname = "Test",
      Avatar = "avatar.png"
    };

    var result = await controller.SignUp(request);

    Assert.NotNull(result.Value);
    var response = Assert.IsType<SignUpResponse>(result.Value);
    Assert.True(response.Success);
  }

  [Fact]
  public async Task ResetPassword_ReturnsNotFound_WhenUserDoesNotExist()
  {
    var (signInManagerMock, _) = CreateSignInManagerWithUserManagerMock();
    var authServiceMock = new Mock<IAuthService>();
    var userManagerMock = new Mock<UserManager<User>>(
      Mock.Of<IUserStore<User>>(), null, null, null, null, null, null, null, null);
    var roleServiceMock = new Mock<IRoleService>();
    var userServiceMock = new Mock<IUserService>();

    userServiceMock.Setup(x => x.GetUserProfileByEmail("nonexistent@example.com"))
      .ReturnsAsync((User?)null);

    var controller = new AuthController(
      signInManagerMock.Object,
      authServiceMock.Object,
      userManagerMock.Object,
      roleServiceMock.Object,
      userServiceMock.Object);

    var request = new ResetPasswordRequest
    {
      Email = "nonexistent@example.com",
      NewPassword = "NewPassword123!"
    };

    var result = await controller.ResetPassword(request);

    Assert.IsType<NotFoundObjectResult>(result.Result);
  }

  [Fact]
  public async Task ResetPassword_ReturnsOk_WhenPasswordIsResetSuccessfully()
  {
    var (signInManagerMock, _) = CreateSignInManagerWithUserManagerMock();
    var authServiceMock = new Mock<IAuthService>();
    var userManagerMock = new Mock<UserManager<User>>(
      Mock.Of<IUserStore<User>>(), null, null, null, null, null, null, null, null);
    var roleServiceMock = new Mock<IRoleService>();
    var userServiceMock = new Mock<IUserService>();

    var user = new User
    {
      Id = 1,
      Email = "test@example.com",
      UserName = "testuser"
    };

    userServiceMock.Setup(x => x.GetUserProfileByEmail("test@example.com"))
      .ReturnsAsync(user);
    userManagerMock.Setup(x => x.GeneratePasswordResetTokenAsync(user))
      .ReturnsAsync("reset-token");
    userManagerMock.Setup(x => x.ResetPasswordAsync(user, It.IsAny<string>(), "NewPassword123!"))
      .ReturnsAsync(IdentityResult.Success);

    var controller = new AuthController(
      signInManagerMock.Object,
      authServiceMock.Object,
      userManagerMock.Object,
      roleServiceMock.Object,
      userServiceMock.Object);

    var request = new ResetPasswordRequest
    {
      Email = "test@example.com",
      NewPassword = "NewPassword123!"
    };

    var result = await controller.ResetPassword(request);

    Assert.NotNull(result.Value);
    var response = Assert.IsType<ResetPasswordResponse>(result.Value);
    Assert.True(response.Success);
  }
}


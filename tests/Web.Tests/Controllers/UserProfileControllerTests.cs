using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;
using WordRush.Core.Features;
using WordRush.Repository.Models;
using WordRush.Web.Controllers;

namespace WordRush.Web.Tests.Controllers;

public class UserProfileControllerTests
{
  private UserProfileController CreateControllerWithContext(IUserService userService, ClaimsPrincipal? user = null)
  {
    var controller = new UserProfileController(userService);
    var context = new DefaultHttpContext();
    if (user != null)
    {
      context.User = user;
    }
    controller.ControllerContext = new ControllerContext
    {
      HttpContext = context
    };
    return controller;
  }

  private dynamic CreateUpdateUserRequest(int id, string email, string nickname, string avatar)
  {
    var requestType = typeof(WordRush.Web.Controllers.UserProfileController).Assembly
      .GetTypes()
      .FirstOrDefault(t => t.Name == "UpdateUserRequest")!;
    var request = Activator.CreateInstance(requestType)!;
    requestType.GetProperty("Id")!.SetValue(request, id);
    requestType.GetProperty("Email")!.SetValue(request, email);
    requestType.GetProperty("Nickname")!.SetValue(request, nickname);
    requestType.GetProperty("Avatar")!.SetValue(request, avatar);
    return request;
  }

  [Fact]
  public async Task GetUserProfile_ReturnsOk_WhenUserExists()
  {
    var userServiceMock = new Mock<IUserService>();
    var user = new User
    {
      Id = 1,
      Email = "test@example.com",
      UserName = "testuser",
      Nickname = "Test",
      Avatar = "avatar.png"
    };

    userServiceMock.Setup(x => x.GetUserProfileByEmail("test@example.com"))
      .ReturnsAsync(user);

    var controller = CreateControllerWithContext(userServiceMock.Object);

    var result = await controller.GetUserProfile("test@example.com");

    var okResult = Assert.IsType<OkObjectResult>(result.Result);
    var returnedUser = Assert.IsType<User>(okResult.Value);
    Assert.Equal("test@example.com", returnedUser.Email);
  }

  [Fact]
  public async Task GetUserProfile_ReturnsBadRequest_WhenEmailIsInvalid()
  {
    var userServiceMock = new Mock<IUserService>();
    var controller = CreateControllerWithContext(userServiceMock.Object);

    var result = await controller.GetUserProfile("invalid-email");

    Assert.IsType<BadRequestObjectResult>(result.Result);
  }

  [Fact]
  public async Task GetUserProfile_ReturnsNotFound_WhenUserDoesNotExist()
  {
    var userServiceMock = new Mock<IUserService>();
    userServiceMock.Setup(x => x.GetUserProfileByEmail("nonexistent@example.com"))
      .ReturnsAsync((User?)null);

    var controller = CreateControllerWithContext(userServiceMock.Object);

    var result = await controller.GetUserProfile("nonexistent@example.com");

    Assert.IsType<NotFoundObjectResult>(result.Result);
  }

  [Fact]
  public async Task UpdateUserProfile_ReturnsOk_WhenUpdateSucceeds()
  {
    var userServiceMock = new Mock<IUserService>();
    var user = new User
    {
      Id = 1,
      Email = "test@example.com",
      UserName = "testuser",
      Nickname = "UpdatedNickname",
      Avatar = "updated-avatar.png"
    };

    userServiceMock.Setup(x => x.UpdateUserProfile(1, "UpdatedNickname", "updated-avatar.png", "test@example.com"))
      .ReturnsAsync(user);

    var controller = CreateControllerWithContext(userServiceMock.Object);

    var request = CreateUpdateUserRequest(1, "test@example.com", "UpdatedNickname", "updated-avatar.png");

    var result = await controller.UpdateUserProfile(request);

    var okResult = Assert.IsType<OkObjectResult>(result.Result);
    var returnedUser = Assert.IsType<User>(okResult.Value);
    Assert.Equal("UpdatedNickname", returnedUser.Nickname);
  }

  [Fact]
  public async Task UpdateUserProfile_ReturnsNotFound_WhenEmailIsInvalid()
  {
    var userServiceMock = new Mock<IUserService>();
    var controller = CreateControllerWithContext(userServiceMock.Object);

    var request = CreateUpdateUserRequest(1, "invalid-email", "Test", "avatar.png");

    var result = await controller.UpdateUserProfile(request);

    Assert.IsType<NotFoundObjectResult>(result.Result);
  }

  [Fact]
  public async Task UpdateUserProfile_ReturnsBadRequest_WhenUserDoesNotExist()
  {
    var userServiceMock = new Mock<IUserService>();
    userServiceMock.Setup(x => x.UpdateUserProfile(999, "Test", "avatar.png", "test@example.com"))
      .ReturnsAsync((User?)null);

    var controller = CreateControllerWithContext(userServiceMock.Object);

    var request = CreateUpdateUserRequest(999, "test@example.com", "Test", "avatar.png");

    var result = await controller.UpdateUserProfile(request);

    Assert.IsType<BadRequestObjectResult>(result.Result);
  }
}


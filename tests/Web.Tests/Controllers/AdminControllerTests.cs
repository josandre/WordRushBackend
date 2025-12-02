using Microsoft.AspNetCore.Mvc;
using Moq;
using WordRush.Core.Features.Admin;
using WordRush.Web.Controllers;

namespace WordRush.Web.Tests.Controllers;

public class AdminControllerTests
{
  [Fact]
  public async Task GetUsers_ReturnsOk_WithUserList()
  {
    var adminServiceMock = new Mock<IAdminService>();
    var users = new List<AdminUserDto>
    {
      new AdminUserDto
      {
        Id = 1,
        Email = "user1@example.com",
        Nickname = "User1",
        RoleId = 1,
        IsActive = true
      }
    };

    adminServiceMock.Setup(x => x.GetUsersAsync(null, null, true))
      .ReturnsAsync(users);

    var controller = new AdminController(adminServiceMock.Object);

    var result = await controller.GetUsers();

    var okResult = Assert.IsType<OkObjectResult>(result.Result);
    var returnedUsers = Assert.IsAssignableFrom<IEnumerable<AdminUserDto>>(okResult.Value);
    Assert.Single(returnedUsers);
  }

  [Fact]
  public async Task GetUsers_AppliesSearchFilter()
  {
    var adminServiceMock = new Mock<IAdminService>();
    var users = new List<AdminUserDto>
    {
      new AdminUserDto
      {
        Id = 1,
        Email = "john@example.com",
        Nickname = "John",
        RoleId = 1,
        IsActive = true
      }
    };

    adminServiceMock.Setup(x => x.GetUsersAsync("john", null, true))
      .ReturnsAsync(users);

    var controller = new AdminController(adminServiceMock.Object);

    var result = await controller.GetUsers("john");

    var okResult = Assert.IsType<OkObjectResult>(result.Result);
    var returnedUsers = Assert.IsAssignableFrom<IEnumerable<AdminUserDto>>(okResult.Value);
    Assert.Single(returnedUsers);
  }

  [Fact]
  public async Task ToggleActive_ReturnsNoContent_WhenUserExists()
  {
    var adminServiceMock = new Mock<IAdminService>();
    adminServiceMock.Setup(x => x.ToggleUserActiveAsync(1))
      .ReturnsAsync(true);

    var controller = new AdminController(adminServiceMock.Object);

    var result = await controller.ToggleActive(1);

    Assert.IsType<NoContentResult>(result);
  }

  [Fact]
  public async Task ToggleActive_ReturnsNotFound_WhenUserDoesNotExist()
  {
    var adminServiceMock = new Mock<IAdminService>();
    adminServiceMock.Setup(x => x.ToggleUserActiveAsync(999))
      .ReturnsAsync(false);

    var controller = new AdminController(adminServiceMock.Object);

    var result = await controller.ToggleActive(999);

    Assert.IsType<NotFoundResult>(result);
  }

  [Fact]
  public async Task SetRole_ReturnsNoContent_WhenRoleIsSet()
  {
    var adminServiceMock = new Mock<IAdminService>();
    adminServiceMock.Setup(x => x.SetUserRoleAsync(1, 2))
      .ReturnsAsync(true);

    var controller = new AdminController(adminServiceMock.Object);

    var result = await controller.SetRole(1, 2);

    Assert.IsType<NoContentResult>(result);
  }

  [Fact]
  public async Task SetRole_ReturnsBadRequest_WhenRoleCannotBeSet()
  {
    var adminServiceMock = new Mock<IAdminService>();
    adminServiceMock.Setup(x => x.SetUserRoleAsync(1, 2))
      .ReturnsAsync(false);

    var controller = new AdminController(adminServiceMock.Object);

    var result = await controller.SetRole(1, 2);

    var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
    Assert.Contains("Unable to change role", badRequestResult.Value?.ToString() ?? string.Empty);
  }
}


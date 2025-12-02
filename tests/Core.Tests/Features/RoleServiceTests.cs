using Microsoft.EntityFrameworkCore;
using WordRush.Core.Features;
using WordRush.Repository;
using WordRush.Repository.Models;

namespace WordRush.Core.Tests.Features;

public class RoleServiceTests
{
  private AppDbContext CreateDbContext()
  {
    var options = new DbContextOptionsBuilder<AppDbContext>()
      .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
      .Options;

    return new AppDbContext(options);
  }

  [Fact]
  public async Task GetRoleById_ReturnsRole_WhenRoleExists()
  {
    // Arrange
    using var context = CreateDbContext();
    var service = new RoleService(context);

    var role = new Role
    {
      Id = 1,
      Name = "Admin"
    };

    context.Roles.Add(role);
    await context.SaveChangesAsync();

    // Act
    var result = await service.GetRoleById(1);

    // Assert
    Assert.NotNull(result);
    Assert.Equal(1, result.Id);
    Assert.Equal("Admin", result.Name);
  }

  [Fact]
  public async Task GetRoleById_ReturnsNull_WhenRoleDoesNotExist()
  {
    // Arrange
    using var context = CreateDbContext();
    var service = new RoleService(context);

    // Act
    var result = await service.GetRoleById(999);

    // Assert
    Assert.Null(result);
  }
}


using Microsoft.EntityFrameworkCore;
using WordRush.Core.Features.Admin;
using WordRush.Repository;
using WordRush.Repository.Models;

namespace WordRush.Core.Tests.Features;

public class AdminServiceTests
{
  private AppDbContext CreateDbContext()
  {
    var options = new DbContextOptionsBuilder<AppDbContext>()
      .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
      .Options;

    return new AppDbContext(options);
  }

  [Fact]
  public async Task GetUsersAsync_ReturnsAllUsers_WhenNoSearchTerm()
  {
    // Arrange
    using var context = CreateDbContext();
    var service = new AdminService(context);

    var users = new List<User>
    {
      new User
      {
        Id = 1,
        Email = "user1@example.com",
        Nickname = "User1",
        Avatar = "avatar1.png",
        RoleId = 1,
        CreatedOn = DateTime.UtcNow.AddDays(-10),
        UpdatedOn = DateTime.UtcNow,
        IsActive = true
      },
      new User
      {
        Id = 2,
        Email = "user2@example.com",
        Nickname = "User2",
        Avatar = "avatar2.png",
        RoleId = 2,
        CreatedOn = DateTime.UtcNow.AddDays(-5),
        UpdatedOn = DateTime.UtcNow,
        IsActive = true
      }
    };

    context.Users.AddRange(users);
    await context.SaveChangesAsync();

    // Act
    var result = await service.GetUsersAsync(string.Empty, "id", true);

    // Assert
    Assert.Equal(2, result.Count);
  }

  [Fact(Skip = "ILike method is PostgreSQL-specific and not supported by InMemory provider. Use integration tests with real database.")]
  public async Task GetUsersAsync_ReturnsFilteredUsers_WhenSearchTermProvided()
  {
    Assert.True(true);
  }

  [Fact]
  public async Task GetUsersAsync_SortsByNickname_Ascending()
  {
    // Arrange
    using var context = CreateDbContext();
    var service = new AdminService(context);

    var users = new List<User>
    {
      new User
      {
        Id = 1,
        Email = "user1@example.com",
        Nickname = "Zebra",
        Avatar = "avatar1.png",
        RoleId = 1,
        CreatedOn = DateTime.UtcNow,
        UpdatedOn = DateTime.UtcNow,
        IsActive = true
      },
      new User
      {
        Id = 2,
        Email = "user2@example.com",
        Nickname = "Alpha",
        Avatar = "avatar2.png",
        RoleId = 2,
        CreatedOn = DateTime.UtcNow,
        UpdatedOn = DateTime.UtcNow,
        IsActive = true
      }
    };

    context.Users.AddRange(users);
    await context.SaveChangesAsync();

    // Act
    var result = await service.GetUsersAsync(string.Empty, "nickname", true);

    // Assert
    Assert.Equal(2, result.Count);
    Assert.Equal("Alpha", result[0].Nickname);
    Assert.Equal("Zebra", result[1].Nickname);
  }

  [Fact]
  public async Task GetUsersAsync_SortsByEmail_Descending()
  {
    // Arrange
    using var context = CreateDbContext();
    var service = new AdminService(context);

    var users = new List<User>
    {
      new User
      {
        Id = 1,
        Email = "alpha@example.com",
        Nickname = "User1",
        Avatar = "avatar1.png",
        RoleId = 1,
        CreatedOn = DateTime.UtcNow,
        UpdatedOn = DateTime.UtcNow,
        IsActive = true
      },
      new User
      {
        Id = 2,
        Email = "zebra@example.com",
        Nickname = "User2",
        Avatar = "avatar2.png",
        RoleId = 2,
        CreatedOn = DateTime.UtcNow,
        UpdatedOn = DateTime.UtcNow,
        IsActive = true
      }
    };

    context.Users.AddRange(users);
    await context.SaveChangesAsync();

    // Act
    var result = await service.GetUsersAsync(string.Empty, "email", false);

    // Assert
    Assert.Equal(2, result.Count);
    Assert.Equal("zebra@example.com", result[0].Email);
    Assert.Equal("alpha@example.com", result[1].Email);
  }

  [Fact]
  public async Task GetUsersAsync_IncludesGameStatistics()
  {
    // Arrange
    using var context = CreateDbContext();
    var service = new AdminService(context);

    var user = new User
    {
      Id = 1,
      Email = "user1@example.com",
      Nickname = "User1",
      Avatar = "avatar1.png",
      RoleId = 1,
      CreatedOn = DateTime.UtcNow,
      UpdatedOn = DateTime.UtcNow,
      IsActive = true,
      GameStatistics = new GameStatistics
      {
        UserId = 1,
        TotalPlayedGame = 10,
        WonGames = 5,
        TotalStore = 150
      }
    };

    context.Users.Add(user);
    await context.SaveChangesAsync();

    // Act
    var result = await service.GetUsersAsync(string.Empty, "id", true);

    // Assert
    Assert.Single(result);
    Assert.Equal(10, result[0].TotalPlayedGame);
    Assert.Equal(5, result[0].WonGames);
    Assert.Equal(150, result[0].TotalStore);
  }

  [Fact]
  public async Task ToggleUserActiveAsync_TogglesActiveStatus()
  {
    // Arrange
    using var context = CreateDbContext();
    var service = new AdminService(context);

    var user = new User
    {
      Id = 1,
      Email = "user1@example.com",
      Nickname = "User1",
      Avatar = "avatar1.png",
      RoleId = 1,
      CreatedOn = DateTime.UtcNow,
      UpdatedOn = DateTime.UtcNow,
      IsActive = true
    };

    context.Users.Add(user);
    await context.SaveChangesAsync();

    // Act
    var result = await service.ToggleUserActiveAsync(1);

    // Assert
    Assert.True(result);
    var updatedUser = await context.Users.FindAsync(1);
    Assert.NotNull(updatedUser);
    Assert.False(updatedUser.IsActive);
  }

  [Fact]
  public async Task ToggleUserActiveAsync_ReturnsFalse_WhenUserDoesNotExist()
  {
    // Arrange
    using var context = CreateDbContext();
    var service = new AdminService(context);

    // Act
    var result = await service.ToggleUserActiveAsync(999);

    // Assert
    Assert.False(result);
  }

  [Fact]
  public async Task SetUserRoleAsync_UpdatesRole_WhenUserExists()
  {
    // Arrange
    using var context = CreateDbContext();
    var service = new AdminService(context);

    var user = new User
    {
      Id = 1,
      Email = "user1@example.com",
      Nickname = "User1",
      Avatar = "avatar1.png",
      RoleId = 1,
      CreatedOn = DateTime.UtcNow,
      UpdatedOn = DateTime.UtcNow,
      IsActive = true
    };

    context.Users.Add(user);
    await context.SaveChangesAsync();

    // Act
    var result = await service.SetUserRoleAsync(1, 2);

    // Assert
    Assert.True(result);
    var updatedUser = await context.Users.FindAsync(1);
    Assert.NotNull(updatedUser);
    Assert.Equal(2, updatedUser.RoleId);
  }

  [Fact]
  public async Task SetUserRoleAsync_ReturnsFalse_WhenUserDoesNotExist()
  {
    // Arrange
    using var context = CreateDbContext();
    var service = new AdminService(context);

    // Act
    var result = await service.SetUserRoleAsync(999, 2);

    // Assert
    Assert.False(result);
  }

  [Fact]
  public async Task SetUserRoleAsync_PreventsRemovingLastAdmin()
  {
    // Arrange
    using var context = CreateDbContext();
    var service = new AdminService(context);

    var adminUser = new User
    {
      Id = 1,
      Email = "admin@example.com",
      Nickname = "Admin",
      Avatar = "avatar.png",
      RoleId = 2, // Admin role
      CreatedOn = DateTime.UtcNow,
      UpdatedOn = DateTime.UtcNow,
      IsActive = true
    };

    context.Users.Add(adminUser);
    await context.SaveChangesAsync();

    var result = await service.SetUserRoleAsync(1, 1);

    // Assert
    Assert.False(result);
    var user = await context.Users.FindAsync(1);
    Assert.NotNull(user);
    Assert.Equal(2, user.RoleId); // Role should remain unchanged
  }

  [Fact]
  public async Task SetUserRoleAsync_AllowsRemovingAdmin_WhenOtherAdminsExist()
  {
    // Arrange
    using var context = CreateDbContext();
    var service = new AdminService(context);

    var adminUsers = new List<User>
    {
      new User
      {
        Id = 1,
        Email = "admin1@example.com",
        Nickname = "Admin1",
        Avatar = "avatar1.png",
        RoleId = 2, // Admin role
        CreatedOn = DateTime.UtcNow,
        UpdatedOn = DateTime.UtcNow,
        IsActive = true
      },
      new User
      {
        Id = 2,
        Email = "admin2@example.com",
        Nickname = "Admin2",
        Avatar = "avatar2.png",
        RoleId = 2, // Admin role
        CreatedOn = DateTime.UtcNow,
        UpdatedOn = DateTime.UtcNow,
        IsActive = true
      }
    };

    context.Users.AddRange(adminUsers);
    await context.SaveChangesAsync();

    var result = await service.SetUserRoleAsync(1, 1);

    // Assert
    Assert.True(result);
    var user = await context.Users.FindAsync(1);
    Assert.NotNull(user);
    Assert.Equal(1, user.RoleId); // Role should be changed
  }
}


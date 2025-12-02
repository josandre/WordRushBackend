using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using WordRush.Core.Features.Scoring;
using WordRush.Repository;
using WordRush.Repository.Models;

namespace WordRush.Core.Tests.Features;

public class GameStatisticsServiceTests
{
  private AppDbContext CreateDbContext()
  {
    var options = new DbContextOptionsBuilder<AppDbContext>()
      .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
      .Options;

    return new AppDbContext(options);
  }

  [Fact]
  public async Task UpdateGameStatisticsAsync_CreatesNewStatistics_WhenNoneExist()
  {
    // Arrange
    using var context = CreateDbContext();
    var loggerMock = new Mock<ILogger<GameStatisticsService>>();
    var service = new GameStatisticsService(context, loggerMock.Object);

    // Act
    await service.UpdateGameStatisticsAsync(1, true, 100);

    // Assert
    var stats = await context.GameStatistics.FirstOrDefaultAsync(gs => gs.UserId == 1);
    Assert.NotNull(stats);
    Assert.Equal(1, stats.TotalPlayedGame);
    Assert.Equal(1, stats.WonGames);
    Assert.Equal(100, stats.TotalStore);
  }

  [Fact]
  public async Task UpdateGameStatisticsAsync_UpdatesExistingStatistics_WhenStatisticsExist()
  {
    // Arrange
    using var context = CreateDbContext();
    var loggerMock = new Mock<ILogger<GameStatisticsService>>();
    var service = new GameStatisticsService(context, loggerMock.Object);

    var existingStats = new GameStatistics
    {
      UserId = 1,
      TotalPlayedGame = 5,
      WonGames = 2,
      TotalStore = 500
    };

    context.GameStatistics.Add(existingStats);
    await context.SaveChangesAsync();

    // Act
    await service.UpdateGameStatisticsAsync(1, true, 150);

    // Assert
    var stats = await context.GameStatistics.FirstOrDefaultAsync(gs => gs.UserId == 1);
    Assert.NotNull(stats);
    Assert.Equal(6, stats.TotalPlayedGame);
    Assert.Equal(3, stats.WonGames);
    Assert.Equal(650, stats.TotalStore);
  }

  [Fact]
  public async Task UpdateGameStatisticsAsync_DoesNotIncrementWonGames_WhenPlayerDidNotWin()
  {
    // Arrange
    using var context = CreateDbContext();
    var loggerMock = new Mock<ILogger<GameStatisticsService>>();
    var service = new GameStatisticsService(context, loggerMock.Object);

    var existingStats = new GameStatistics
    {
      UserId = 1,
      TotalPlayedGame = 5,
      WonGames = 2,
      TotalStore = 500
    };

    context.GameStatistics.Add(existingStats);
    await context.SaveChangesAsync();

    // Act
    await service.UpdateGameStatisticsAsync(1, false, 80);

    // Assert
    var stats = await context.GameStatistics.FirstOrDefaultAsync(gs => gs.UserId == 1);
    Assert.NotNull(stats);
    Assert.Equal(6, stats.TotalPlayedGame);
    Assert.Equal(2, stats.WonGames);
    Assert.Equal(580, stats.TotalStore);
  }

  [Fact]
  public async Task UpdateGameStatisticsAsync_AddsScoreToTotalStore()
  {
    // Arrange
    using var context = CreateDbContext();
    var loggerMock = new Mock<ILogger<GameStatisticsService>>();
    var service = new GameStatisticsService(context, loggerMock.Object);

    var existingStats = new GameStatistics
    {
      UserId = 1,
      TotalPlayedGame = 1,
      WonGames = 0,
      TotalStore = 50
    };

    context.GameStatistics.Add(existingStats);
    await context.SaveChangesAsync();

    // Act
    await service.UpdateGameStatisticsAsync(1, false, 75);

    // Assert
    var stats = await context.GameStatistics.FirstOrDefaultAsync(gs => gs.UserId == 1);
    Assert.NotNull(stats);
    Assert.Equal(125, stats.TotalStore);
  }

  [Fact]
  public async Task GetGameStatisticsByUserIdAsync_ReturnsStatistics_WhenTheyExist()
  {
    // Arrange
    using var context = CreateDbContext();
    var loggerMock = new Mock<ILogger<GameStatisticsService>>();
    var service = new GameStatisticsService(context, loggerMock.Object);

    var stats = new GameStatistics
    {
      UserId = 1,
      TotalPlayedGame = 10,
      WonGames = 5,
      TotalStore = 1000
    };

    context.GameStatistics.Add(stats);
    await context.SaveChangesAsync();

    // Act
    var result = await service.GetGameStatisticsByUserIdAsync(1);

    // Assert
    Assert.NotNull(result);
    Assert.Equal(10, result.TotalPlayedGame);
    Assert.Equal(5, result.WonGames);
    Assert.Equal(1000, result.TotalStore);
  }

  [Fact]
  public async Task GetGameStatisticsByUserIdAsync_ReturnsNull_WhenStatisticsDoNotExist()
  {
    // Arrange
    using var context = CreateDbContext();
    var loggerMock = new Mock<ILogger<GameStatisticsService>>();
    var service = new GameStatisticsService(context, loggerMock.Object);

    // Act
    var result = await service.GetGameStatisticsByUserIdAsync(999);

    // Assert
    Assert.Null(result);
  }
}


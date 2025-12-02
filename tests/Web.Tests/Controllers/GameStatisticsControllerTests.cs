using Microsoft.AspNetCore.Mvc;
using Moq;
using WordRush.Core.Features.Scoring;
using WordRush.Repository.Models;
using WordRush.Web.Controllers;
using WordRush.Web.Models;

namespace WordRush.Web.Tests.Controllers;

public class GameStatisticsControllerTests
{
  [Fact]
  public async Task GetStatisticsByUserId_ReturnsOk_WhenStatisticsExist()
  {
    var gameStatisticsServiceMock = new Mock<IGameStatisticsService>();
    var statistics = new GameStatistics
    {
      UserId = 1,
      TotalPlayedGame = 10,
      WonGames = 5,
      TotalStore = 150
    };

    gameStatisticsServiceMock.Setup(x => x.GetGameStatisticsByUserIdAsync(1))
      .ReturnsAsync(statistics);

    var controller = new GameStatisticsController(gameStatisticsServiceMock.Object);

    var result = await controller.GetStatisticsByUserId(1);

    var okResult = Assert.IsType<OkObjectResult>(result);
    var response = Assert.IsType<GameStatisticsResponse>(okResult.Value);
    Assert.Equal(10, response.TotalPlayedGame);
    Assert.Equal(5, response.WonGames);
    Assert.Equal(150, response.TotalStore);
  }

  [Fact]
  public async Task GetStatisticsByUserId_ReturnsNotFound_WhenStatisticsDoNotExist()
  {
    var gameStatisticsServiceMock = new Mock<IGameStatisticsService>();
    gameStatisticsServiceMock.Setup(x => x.GetGameStatisticsByUserIdAsync(999))
      .ReturnsAsync((GameStatistics?)null);

    var controller = new GameStatisticsController(gameStatisticsServiceMock.Object);

    var result = await controller.GetStatisticsByUserId(999);

    var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
    Assert.Contains("No game statistics found", notFoundResult.Value?.ToString() ?? string.Empty);
  }
}


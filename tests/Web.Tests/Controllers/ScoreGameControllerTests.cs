using Microsoft.AspNetCore.Mvc;
using Moq;
using WordRush.Core.Features.Scoring;
using WordRush.Core.Features.Scoring.Models;
using WordRush.Web.Controllers;

namespace WordRush.Web.Tests.Controllers;

public class ScoreGameControllerTests
{
  [Fact]
  public async Task Score_ReturnsOk_WhenScoringSucceeds()
  {
    var scoringServiceMock = new Mock<IScoringService>();
    var response = new StopGameResponse
    {
      Letter = "S",
      Categories = new List<string> { "Animal" },
      Players = new List<PlayerResult>
      {
        new PlayerResult
        {
          Name = "John",
          Total = 10
        }
      }
    };

    scoringServiceMock.Setup(x => x.ScoreGameAsync(It.IsAny<StopGameRequest>()))
      .ReturnsAsync(response);

    var controller = new ScoreGameController(scoringServiceMock.Object);

    var request = new StopGameRequest
    {
      Letter = "S",
      Categories = new List<string> { "Animal" },
      Players = new List<PlayerEntry>
      {
        new PlayerEntry
        {
          Name = "John",
          Answers = new Dictionary<string, string> { { "Animal", "Snake" } }
        }
      }
    };

    var result = await controller.Score(request);

    var okResult = Assert.IsType<OkObjectResult>(result);
    var returnedResponse = Assert.IsType<StopGameResponse>(okResult.Value);
    Assert.Equal("S", returnedResponse.Letter);
  }

  [Fact]
  public async Task Score_ReturnsBadRequest_WhenModelStateIsInvalid()
  {
    var scoringServiceMock = new Mock<IScoringService>();
    var controller = new ScoreGameController(scoringServiceMock.Object);
    controller.ModelState.AddModelError("Letter", "Letter is required");

    var request = new StopGameRequest
    {
      Letter = string.Empty,
      Categories = new List<string>(),
      Players = new List<PlayerEntry>()
    };

    var result = await controller.Score(request);

    Assert.IsType<BadRequestObjectResult>(result);
  }

  [Fact]
  public async Task Score_ReturnsBadRequest_WhenRequestIsNull()
  {
    var scoringServiceMock = new Mock<IScoringService>();
    var controller = new ScoreGameController(scoringServiceMock.Object);

    var result = await controller.Score(null!);

    Assert.IsType<BadRequestObjectResult>(result);
  }

  [Fact]
  public async Task Score_ReturnsInternalServerError_WhenScoringReturnsNull()
  {
    var scoringServiceMock = new Mock<IScoringService>();
    scoringServiceMock.Setup(x => x.ScoreGameAsync(It.IsAny<StopGameRequest>()))
      .ReturnsAsync((StopGameResponse?)null);

    var controller = new ScoreGameController(scoringServiceMock.Object);

    var request = new StopGameRequest
    {
      Letter = "S",
      Categories = new List<string> { "Animal" },
      Players = new List<PlayerEntry>()
    };

    var result = await controller.Score(request);

    var statusCodeResult = Assert.IsType<ObjectResult>(result);
    Assert.Equal(500, statusCodeResult.StatusCode);
  }
}


using Microsoft.AspNetCore.Mvc;
using Moq;
using WordRush.Core.Features.Game;
using WordRush.Core.Features.Realtime;
using WordRush.Core.Features.Realtime.Models;
using WordRush.Web.Features.Game;
using WordRush.Web.Features.Game.Models;

namespace WordRush.Web.Tests.Features.Game;

public class GameControllerTests
{
  [Fact]
  public async Task UpdateGameSettings_ReturnsOk_WhenSettingsAreUpdated()
  {
    var gameSettingsServiceMock = new Mock<IGameSettingsService>();
    var room = new GameRoom("room123");
    room.Settings = new GameSettings
    {
      TimeLimit = 60,
      HintTokens = 3,
      Letters = new[] { "A", "B", "C" }
    };

    gameSettingsServiceMock.Setup(x => x.UpdateGameSettings("room123", It.IsAny<GameSettings>()))
      .ReturnsAsync(room);

    var controller = new GameController(gameSettingsServiceMock.Object);

    var request = new UpdateGameSettingsRequest
    {
      RoomId = "room123",
      Settings = new GameSettings
      {
        TimeLimit = 90,
        HintTokens = 5,
        Letters = new[] { "A", "B", "C", "D" }
      }
    };

    var result = await controller.UpdateGameSettings(request);

    var okResult = Assert.IsType<OkObjectResult>(result.Result);
    var response = Assert.IsType<UpdateGameSettingsResponse>(okResult.Value);
    Assert.Equal("room123", response.RoomId);
    Assert.Equal(60, response.Settings.TimeLimit);
  }

  [Fact]
  public async Task UpdateGameSettings_ReturnsBadRequest_WhenModelStateIsInvalid()
  {
    var gameSettingsServiceMock = new Mock<IGameSettingsService>();
    var controller = new GameController(gameSettingsServiceMock.Object);
    controller.ModelState.AddModelError("RoomId", "RoomId is required");

    var request = new UpdateGameSettingsRequest
    {
      RoomId = string.Empty,
      Settings = new GameSettings()
    };

    var result = await controller.UpdateGameSettings(request);

    Assert.IsType<BadRequestObjectResult>(result.Result);
  }

  [Fact]
  public async Task UpdateGameSettings_ReturnsBadRequest_WhenRoomIdIsEmpty()
  {
    var gameSettingsServiceMock = new Mock<IGameSettingsService>();
    var controller = new GameController(gameSettingsServiceMock.Object);

    var request = new UpdateGameSettingsRequest
    {
      RoomId = string.Empty,
      Settings = new GameSettings()
    };

    var result = await controller.UpdateGameSettings(request);

    var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
    Assert.Contains("RoomId is required", badRequestResult.Value?.ToString() ?? string.Empty);
  }

  [Fact]
  public async Task UpdateGameSettings_TruncatesLettersArray_WhenArrayExceedsMaximum()
  {
    var gameSettingsServiceMock = new Mock<IGameSettingsService>();
    var room = new GameRoom("room123");
    gameSettingsServiceMock.Setup(x => x.UpdateGameSettings("room123", It.IsAny<GameSettings>()))
      .ReturnsAsync(room);

    var controller = new GameController(gameSettingsServiceMock.Object);

    var request = new UpdateGameSettingsRequest
    {
      RoomId = "room123",
      Settings = new GameSettings
      {
        Letters = new[] { "A", "B", "C", "D", "E", "F" }
      }
    };

    var result = await controller.UpdateGameSettings(request);

    var okResult = Assert.IsType<OkObjectResult>(result.Result);
    Assert.Equal(5, request.Settings.Letters.Length);
  }

  [Fact]
  public async Task UpdateGameSettings_ReturnsNotFound_WhenRoomDoesNotExist()
  {
    var gameSettingsServiceMock = new Mock<IGameSettingsService>();
    gameSettingsServiceMock.Setup(x => x.UpdateGameSettings("nonexistent", It.IsAny<GameSettings>()))
      .ReturnsAsync((GameRoom?)null);

    var controller = new GameController(gameSettingsServiceMock.Object);

    var request = new UpdateGameSettingsRequest
    {
      RoomId = "nonexistent",
      Settings = new GameSettings()
    };

    var result = await controller.UpdateGameSettings(request);

    var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
    Assert.Contains("Game room with ID 'nonexistent' not found", notFoundResult.Value?.ToString() ?? string.Empty);
  }

  [Fact]
  public async Task UpdateGameSettings_AllowsMaximumFiveLetters()
  {
    var gameSettingsServiceMock = new Mock<IGameSettingsService>();
    var room = new GameRoom("room123");
    gameSettingsServiceMock.Setup(x => x.UpdateGameSettings("room123", It.IsAny<GameSettings>()))
      .ReturnsAsync(room);

    var controller = new GameController(gameSettingsServiceMock.Object);

    var request = new UpdateGameSettingsRequest
    {
      RoomId = "room123",
      Settings = new GameSettings
      {
        Letters = new[] { "A", "B", "C", "D", "E" }
      }
    };

    var result = await controller.UpdateGameSettings(request);

    var okResult = Assert.IsType<OkObjectResult>(result.Result);
    Assert.NotNull(okResult.Value);
  }
}


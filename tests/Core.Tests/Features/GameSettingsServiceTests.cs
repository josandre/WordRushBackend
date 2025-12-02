using Moq;
using WordRush.Core.Features.Game;
using WordRush.Core.Features.Realtime;
using WordRush.Core.Features.Realtime.Models;

namespace WordRush.Core.Tests.Features;

public class GameSettingsServiceTests
{
  [Fact]
  public async Task UpdateGameSettings_ReturnsGameRoom_WhenRoomExists()
  {
    // Arrange
    var webSocketServiceMock = new Mock<IWordRushWebSocketService>();
    var service = new GameSettingsService(webSocketServiceMock.Object);

    var room = new GameRoom("room123");
    var settings = new GameSettings
    {
      TimeLimit = 60,
      HintTokens = 3
    };

    webSocketServiceMock.Setup(x => x.GetRoom("room123"))
      .Returns(room);

    // Act
    var result = await service.UpdateGameSettings("room123", settings);

    // Assert
    Assert.NotNull(result);
    Assert.Equal("room123", result.RoomId);
    Assert.Equal(60, result.Settings.TimeLimit);
    Assert.Equal(3, result.Settings.HintTokens);
  }

  [Fact]
  public async Task UpdateGameSettings_ReturnsNull_WhenRoomDoesNotExist()
  {
    // Arrange
    var webSocketServiceMock = new Mock<IWordRushWebSocketService>();
    var service = new GameSettingsService(webSocketServiceMock.Object);

    var settings = new GameSettings
    {
      TimeLimit = 60,
      HintTokens = 3
    };

    webSocketServiceMock.Setup(x => x.GetRoom("nonexistent"))
      .Returns((GameRoom?)null);

    // Act
    var result = await service.UpdateGameSettings("nonexistent", settings);

    // Assert
    Assert.Null(result);
  }

  [Fact]
  public async Task UpdateGameSettings_ReturnsNull_WhenRoomIdIsEmpty()
  {
    // Arrange
    var webSocketServiceMock = new Mock<IWordRushWebSocketService>();
    var service = new GameSettingsService(webSocketServiceMock.Object);

    var settings = new GameSettings
    {
      TimeLimit = 60,
      HintTokens = 3
    };

    // Act
    var result = await service.UpdateGameSettings(string.Empty, settings);

    // Assert
    Assert.Null(result);
    webSocketServiceMock.Verify(x => x.GetRoom(It.IsAny<string>()), Times.Never);
  }

  [Fact]
  public async Task UpdateGameSettings_ReturnsNull_WhenSettingsIsNull()
  {
    // Arrange
    var webSocketServiceMock = new Mock<IWordRushWebSocketService>();
    var service = new GameSettingsService(webSocketServiceMock.Object);

    // Act
    var result = await service.UpdateGameSettings("room123", null!);

    // Assert
    Assert.Null(result);
    webSocketServiceMock.Verify(x => x.GetRoom(It.IsAny<string>()), Times.Never);
  }

  [Fact]
  public async Task UpdateGameSettings_UpdatesRoomSettings()
  {
    // Arrange
    var webSocketServiceMock = new Mock<IWordRushWebSocketService>();
    var service = new GameSettingsService(webSocketServiceMock.Object);

    var room = new GameRoom("room123");
    room.Settings = new GameSettings
    {
      TimeLimit = 30,
      HintTokens = 1
    };

    var newSettings = new GameSettings
    {
      TimeLimit = 90,
      HintTokens = 5
    };

    webSocketServiceMock.Setup(x => x.GetRoom("room123"))
      .Returns(room);

    // Act
    var result = await service.UpdateGameSettings("room123", newSettings);

    // Assert
    Assert.NotNull(result);
    Assert.Equal(90, result.Settings.TimeLimit);
    Assert.Equal(5, result.Settings.HintTokens);
  }
}


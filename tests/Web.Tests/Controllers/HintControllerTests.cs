using Microsoft.AspNetCore.Mvc;
using Moq;
using WordRush.Core.Features.Hints;
using WordRush.Web.Features.Game;

namespace WordRush.Web.Tests.Controllers;

public class HintControllerTests
{
  [Fact]
  public async Task GetHint_ReturnsOk_WithHint()
  {
    var hintServiceMock = new Mock<IHintService>();
    hintServiceMock.Setup(x => x.GetHintAsync("A", "Animal"))
      .ReturnsAsync("A fast-running herbivore found in African savannas");

    var controller = new HintController(hintServiceMock.Object);

    var result = await controller.GetHint("A", "Animal");

    var okResult = Assert.IsType<OkObjectResult>(result);
    var response = okResult.Value;
    Assert.NotNull(response);
  }

  [Fact]
  public async Task GetHint_ReturnsBadRequest_WhenLetterIsEmpty()
  {
    var hintServiceMock = new Mock<IHintService>();
    var controller = new HintController(hintServiceMock.Object);

    var result = await controller.GetHint(string.Empty, "Animal");

    Assert.IsType<BadRequestObjectResult>(result);
  }

  [Fact]
  public async Task GetHint_ReturnsBadRequest_WhenCategoryIsEmpty()
  {
    var hintServiceMock = new Mock<IHintService>();
    var controller = new HintController(hintServiceMock.Object);

    var result = await controller.GetHint("A", string.Empty);

    Assert.IsType<BadRequestObjectResult>(result);
  }

  [Fact]
  public async Task GetHint_ReturnsInternalServerError_WhenExceptionOccurs()
  {
    var hintServiceMock = new Mock<IHintService>();
    hintServiceMock.Setup(x => x.GetHintAsync("A", "Animal"))
      .ThrowsAsync(new Exception("Service error"));

    var controller = new HintController(hintServiceMock.Object);

    var result = await controller.GetHint("A", "Animal");

    var statusCodeResult = Assert.IsType<ObjectResult>(result);
    Assert.Equal(500, statusCodeResult.StatusCode);
  }
}


using Microsoft.AspNetCore.Mvc;
using Moq;
using WordRush.Core.Features.Settings;
using WordRush.Web.Controllers;

namespace WordRush.Web.Tests.Controllers;

public class CategoryValidationControllerTests
{
  [Fact]
  public async Task CategoryValidationCheck_ReturnsOk_WithValidationResult()
  {
    var categoryValidationServiceMock = new Mock<ICategoryValidationService>();
    categoryValidationServiceMock.Setup(x => x.GetCategoryValidationAsync("Animals"))
      .ReturnsAsync(true);

    var controller = new CategoryValidationController(categoryValidationServiceMock.Object);

    var result = await controller.CategoryValidationCheck("Animals");

    var okResult = Assert.IsType<OkObjectResult>(result);
    var response = okResult.Value;
    Assert.NotNull(response);
  }

  [Fact]
  public async Task CategoryValidationCheck_ReturnsBadRequest_WhenCategoryIsEmpty()
  {
    var categoryValidationServiceMock = new Mock<ICategoryValidationService>();
    var controller = new CategoryValidationController(categoryValidationServiceMock.Object);

    var result = await controller.CategoryValidationCheck(string.Empty);

    Assert.IsType<BadRequestObjectResult>(result);
  }

  [Fact]
  public async Task CategoryValidationCheck_ReturnsInternalServerError_WhenExceptionOccurs()
  {
    var categoryValidationServiceMock = new Mock<ICategoryValidationService>();
    categoryValidationServiceMock.Setup(x => x.GetCategoryValidationAsync("Animals"))
      .ThrowsAsync(new Exception("Service error"));

    var controller = new CategoryValidationController(categoryValidationServiceMock.Object);

    var result = await controller.CategoryValidationCheck("Animals");

    var statusCodeResult = Assert.IsType<ObjectResult>(result);
    Assert.Equal(500, statusCodeResult.StatusCode);
  }
}


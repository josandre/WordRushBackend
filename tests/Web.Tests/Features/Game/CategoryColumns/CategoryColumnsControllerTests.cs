using Microsoft.AspNetCore.Mvc;
using Moq;
using WordRush.Core.Features.Game.CategoryColumns;
using WordRush.Repository.Models;
using WordRush.Web.Features.Game.CategoryColumns;
using WordRush.Web.Features.Game.CategoryColumns.Models;

namespace WordRush.Web.Tests.Features.Game.CategoryColumns;

public class CategoryColumnsControllerTests
{
  [Fact]
  public async Task GetDefaultCategories_ReturnsOk_WhenCategoriesExist()
  {
    var categoryColumnsServiceMock = new Mock<ICategoryColumns>();
    var categoryType = new CategoryType
    {
      Id = 1,
      Name = "Default",
      CategoryColumns = new List<CategoryColumn>
      {
        new CategoryColumn
        {
          Id = 1,
          Column = "Animal"
        },
        new CategoryColumn
        {
          Id = 2,
          Column = "Food"
        }
      }
    };

    categoryColumnsServiceMock.Setup(x => x.GetDefaultCategories())
      .ReturnsAsync(categoryType);

    var controller = new CategoryColumnsController(categoryColumnsServiceMock.Object);

    var result = await controller.GetDefaultCategories();

    var okResult = Assert.IsType<OkObjectResult>(result.Result);
    var response = Assert.IsType<CategoryColumnsResponse>(okResult.Value);
    Assert.Equal(1, response.CategoryType.Id);
    Assert.Equal("Default", response.CategoryType.Name);
    Assert.Equal(2, response.CategoryType.CategoryColumns.Count);
    Assert.Equal("Animal", response.CategoryType.CategoryColumns[0].Column);
  }

  [Fact]
  public async Task GetDefaultCategories_ReturnsNotFound_WhenNoCategoriesExist()
  {
    var categoryColumnsServiceMock = new Mock<ICategoryColumns>();
    categoryColumnsServiceMock.Setup(x => x.GetDefaultCategories())
      .ReturnsAsync((CategoryType?)null);

    var controller = new CategoryColumnsController(categoryColumnsServiceMock.Object);

    var result = await controller.GetDefaultCategories();

    var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
    Assert.Contains("No default category type found", notFoundResult.Value?.ToString() ?? string.Empty);
  }

  [Fact]
  public async Task GetDefaultCategories_MapsCategoryColumnsCorrectly()
  {
    var categoryColumnsServiceMock = new Mock<ICategoryColumns>();
    var categoryType = new CategoryType
    {
      Id = 2,
      Name = "Custom",
      CategoryColumns = new List<CategoryColumn>
      {
        new CategoryColumn
        {
          Id = 10,
          Column = "Country"
        },
        new CategoryColumn
        {
          Id = 11,
          Column = "Name"
        },
        new CategoryColumn
        {
          Id = 12,
          Column = "Color"
        }
      }
    };

    categoryColumnsServiceMock.Setup(x => x.GetDefaultCategories())
      .ReturnsAsync(categoryType);

    var controller = new CategoryColumnsController(categoryColumnsServiceMock.Object);

    var result = await controller.GetDefaultCategories();

    var okResult = Assert.IsType<OkObjectResult>(result.Result);
    var response = Assert.IsType<CategoryColumnsResponse>(okResult.Value);
    Assert.Equal(3, response.CategoryType.CategoryColumns.Count);
    Assert.Equal(10, response.CategoryType.CategoryColumns[0].Id);
    Assert.Equal("Country", response.CategoryType.CategoryColumns[0].Column);
    Assert.Equal("Name", response.CategoryType.CategoryColumns[1].Column);
    Assert.Equal("Color", response.CategoryType.CategoryColumns[2].Column);
  }
}


using Microsoft.AspNetCore.Mvc;
using Moq;
using WordRush.Core.Features;
using WordRush.Web.Features.Config;

namespace WordRush.Web.Tests.Features.Config;

public class FeatureFlagControllerTests
{
  [Fact]
  public void Get_ReturnsOk_WithFeatureFlags()
  {
    var featureFlagServiceMock = new Mock<IFeatureFlagService>();
    var flags = new Dictionary<string, bool>
    {
      { "feature1", true },
      { "feature2", false },
      { "feature3", true }
    };

    featureFlagServiceMock.Setup(x => x.GetFlags("server"))
      .Returns(flags);

    var controller = new FeatureFlagController(featureFlagServiceMock.Object);

    var result = controller.Get();

    var okResult = Assert.IsType<OkObjectResult>(result.Result);
    var returnedFlags = Assert.IsAssignableFrom<IDictionary<string, bool>>(okResult.Value);
    Assert.Equal(3, returnedFlags.Count);
    Assert.True(returnedFlags["feature1"]);
    Assert.False(returnedFlags["feature2"]);
    Assert.True(returnedFlags["feature3"]);
  }

  [Fact]
  public void Get_ReturnsOk_WithEmptyDictionary_WhenNoFlags()
  {
    var featureFlagServiceMock = new Mock<IFeatureFlagService>();
    var emptyFlags = new Dictionary<string, bool>();

    featureFlagServiceMock.Setup(x => x.GetFlags("server"))
      .Returns(emptyFlags);

    var controller = new FeatureFlagController(featureFlagServiceMock.Object);

    var result = controller.Get();

    var okResult = Assert.IsType<OkObjectResult>(result.Result);
    var returnedFlags = Assert.IsAssignableFrom<IDictionary<string, bool>>(okResult.Value);
    Assert.Empty(returnedFlags);
  }

  [Fact]
  public void Get_CallsServiceWithServerKey()
  {
    var featureFlagServiceMock = new Mock<IFeatureFlagService>();
    featureFlagServiceMock.Setup(x => x.GetFlags("server"))
      .Returns(new Dictionary<string, bool>());

    var controller = new FeatureFlagController(featureFlagServiceMock.Object);

    controller.Get();

    featureFlagServiceMock.Verify(x => x.GetFlags("server"), Times.Once);
  }
}


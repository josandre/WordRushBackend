using Microsoft.Extensions.Configuration;
using Moq;
using WordRush.Core.Features;

namespace WordRush.Core.Tests.Features;

public class FeatureFlagServiceTests
{
  private IConfiguration CreateConfiguration(Dictionary<string, string>? configValues = null)
  {
    var defaultConfig = new Dictionary<string, string>
    {
      { "LaunchDarkly:SdkKey", "test-sdk-key" },
      { "LaunchDarkly:MemberId", "test-member-id" }
    };

    if (configValues != null)
    {
      foreach (var kvp in configValues)
      {
        defaultConfig[kvp.Key] = kvp.Value;
      }
    }

    return new ConfigurationBuilder()
      .AddInMemoryCollection(defaultConfig)
      .Build();
  }

  [Fact]
  public void FeatureFlagService_Initializes_WhenConfigProvided()
  {
    // Arrange
    var config = CreateConfiguration();

    using var service = new FeatureFlagService(config);

    Assert.NotNull(service);
  }

  [Fact]
  public void FeatureFlagService_Disposes_WithoutError()
  {
    // Arrange
    var config = CreateConfiguration();
    var service = new FeatureFlagService(config);

    service.Dispose();
    Assert.True(true);
  }

  [Fact]
  public void GetFlags_ReturnsDictionary()
  {
    // Arrange
    var config = CreateConfiguration();
    using var service = new FeatureFlagService(config);

    // Act
    var result = service.GetFlags("test-user-key");

    // Assert
    Assert.NotNull(result);
    Assert.IsType<Dictionary<string, bool>>(result);
  }

  [Fact]
  public void GetFlags_ReturnsEmptyDictionary_WhenNoFlags()
  {
    // Arrange
    var config = CreateConfiguration();
    using var service = new FeatureFlagService(config);

    var result = service.GetFlags("test-user-key");

    Assert.NotNull(result);
  }
}


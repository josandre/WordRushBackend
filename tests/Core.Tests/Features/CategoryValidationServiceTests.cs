using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Moq;
using Moq.Protected;
using WordRush.Core.Features.Settings;

namespace WordRush.Core.Tests.Features;

public class CategoryValidationServiceTests
{
  private IConfiguration CreateConfiguration(Dictionary<string, string>? configValues = null)
  {
    var defaultConfig = new Dictionary<string, string>
    {
      { "Ollama:Model", "llama3" },
      { "Ollama:BaseUrl", "http://localhost:11434/api/generate" },
      { "Ollama:RequestTimeoutSeconds", "300" },
      { "Ollama:Options:temperature", "0.15" },
      { "Ollama:Options:num_predict", "128" }
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

  private HttpClient CreateHttpClient(HttpResponseMessage responseMessage)
  {
    var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
    handlerMock
      .Protected()
      .Setup<Task<HttpResponseMessage>>(
        "SendAsync",
        ItExpr.IsAny<HttpRequestMessage>(),
        ItExpr.IsAny<CancellationToken>())
      .ReturnsAsync(responseMessage);

    return new HttpClient(handlerMock.Object)
    {
      BaseAddress = new Uri("http://localhost")
    };
  }

  private IHttpClientFactory CreateHttpClientFactory(HttpClient httpClient)
  {
    var factoryMock = new Mock<IHttpClientFactory>();
    factoryMock.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);
    return factoryMock.Object;
  }

  [Fact]
  public async Task GetCategoryValidationAsync_ReturnsNull_WhenCategoryNameIsEmpty()
  {
    // Arrange
    var httpClient = CreateHttpClient(new HttpResponseMessage(HttpStatusCode.OK));
    var httpClientFactory = CreateHttpClientFactory(httpClient);
    var config = CreateConfiguration();
    var service = new CategoryValidationService(httpClientFactory, config);

    // Act
    var result = await service.GetCategoryValidationAsync(string.Empty);

    // Assert
    Assert.Null(result);
  }

  [Fact]
  public async Task GetCategoryValidationAsync_ReturnsTrue_WhenCategoryIsValid()
  {
    // Arrange
    var ollamaResponse = new
    {
      response = """
        {
          "category": "Animals",
          "isValid": true
        }
        """
    };

    var jsonContent = JsonSerializer.Serialize(ollamaResponse);
    var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
    {
      Content = new StringContent(jsonContent, Encoding.UTF8, "application/json")
    };

    var httpClient = CreateHttpClient(httpResponse);
    var httpClientFactory = CreateHttpClientFactory(httpClient);
    var config = CreateConfiguration();
    var service = new CategoryValidationService(httpClientFactory, config);

    // Act
    var result = await service.GetCategoryValidationAsync("Animals");

    // Assert
    Assert.True(result);
  }

  [Fact]
  public async Task GetCategoryValidationAsync_ReturnsFalse_WhenCategoryIsInvalid()
  {
    // Arrange
    var ollamaResponse = new
    {
      response = """
        {
          "category": "Eiffel Tower",
          "isValid": false
        }
        """
    };

    var jsonContent = JsonSerializer.Serialize(ollamaResponse);
    var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
    {
      Content = new StringContent(jsonContent, Encoding.UTF8, "application/json")
    };

    var httpClient = CreateHttpClient(httpResponse);
    var httpClientFactory = CreateHttpClientFactory(httpClient);
    var config = CreateConfiguration();
    var service = new CategoryValidationService(httpClientFactory, config);

    // Act
    var result = await service.GetCategoryValidationAsync("Eiffel Tower");

    // Assert
    Assert.False(result);
  }

  [Fact]
  public async Task GetCategoryValidationAsync_ReturnsFalse_WhenHttpRequestFails()
  {
    // Arrange
    var httpResponse = new HttpResponseMessage(HttpStatusCode.InternalServerError)
    {
      Content = new StringContent("Error", Encoding.UTF8, "text/plain")
    };

    var httpClient = CreateHttpClient(httpResponse);
    var httpClientFactory = CreateHttpClientFactory(httpClient);
    var config = CreateConfiguration();
    var service = new CategoryValidationService(httpClientFactory, config);

    // Act
    var result = await service.GetCategoryValidationAsync("Animals");

    // Assert
    Assert.False(result);
  }

  [Fact]
  public async Task GetCategoryValidationAsync_ReturnsFalse_WhenResponseIsEmpty()
  {
    // Arrange
    var ollamaResponse = new
    {
      response = """
        {
          "category": "Animals",
          "isValid": ""
        }
        """
    };

    var jsonContent = JsonSerializer.Serialize(ollamaResponse);
    var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
    {
      Content = new StringContent(jsonContent, Encoding.UTF8, "application/json")
    };

    var httpClient = CreateHttpClient(httpResponse);
    var httpClientFactory = CreateHttpClientFactory(httpClient);
    var config = CreateConfiguration();
    var service = new CategoryValidationService(httpClientFactory, config);

    // Act
    var result = await service.GetCategoryValidationAsync("Animals");

    // Assert
    Assert.False(result);
  }

  [Fact]
  public void ExtractEmbeddedJson_ReturnsJson_WhenResponsePropertyExists()
  {
    // Arrange
    var jsonResponse = """
      {
        "response": "{\"isValid\": true}"
      }
      """;

    // Act
    var result = CategoryValidationService.ExtractEmbeddedJson(jsonResponse);

    // Assert
    Assert.NotNull(result);
    Assert.Contains("isValid", result);
  }

  [Fact]
  public void ExtractEmbeddedJson_ReturnsEmptyObject_WhenInputIsEmpty()
  {
    // Act
    var result = CategoryValidationService.ExtractEmbeddedJson(string.Empty);

    // Assert
    Assert.Equal("{}", result);
  }

  [Fact]
  public void ExtractEmbeddedJson_HandlesInvalidJson()
  {
    // Arrange
    var invalidJson = "This is not valid JSON";

    var result = CategoryValidationService.ExtractEmbeddedJson(invalidJson);

    Assert.NotNull(result);
  }
}


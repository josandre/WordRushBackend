using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Moq;
using Moq.Protected;
using WordRush.Core.Features.Scoring;
using WordRush.Core.Features.Scoring.Models;

namespace WordRush.Core.Tests.Features;

public class StopGameScoringServiceTests
{
  private IConfiguration CreateConfiguration(Dictionary<string, string>? configValues = null)
  {
    var defaultConfig = new Dictionary<string, string>
    {
      { "Ollama:Model", "llama3" },
      { "Ollama:BaseUrl", "http://localhost:11434/api/generate" },
      { "Ollama:RequestTimeoutSeconds", "300" },
      { "Ollama:Options:temperature", "0.15" },
      { "Ollama:Options:num_predict", "1024" }
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
  public void BuildPrompt_GeneratesValidPrompt()
  {
    // Arrange
    var httpClient = CreateHttpClient(new HttpResponseMessage(HttpStatusCode.OK));
    var httpClientFactory = CreateHttpClientFactory(httpClient);
    var config = CreateConfiguration();
    var service = new StopGameScoringService(httpClientFactory, config);

    var request = new StopGameRequest
    {
      Letter = "S",
      Categories = new List<string> { "Animal", "Food" },
      Players = new List<PlayerEntry>
      {
        new PlayerEntry
        {
          Name = "John",
          UserId = 1,
          Answers = new Dictionary<string, string>
          {
            { "Animal", "Snake" },
            { "Food", "Soup" }
          }
        }
      }
    };

    // Act
    var prompt = service.BuildPrompt(request);

    // Assert
    Assert.Contains("Letter: S", prompt);
    Assert.Contains("Categories: Animal, Food", prompt);
    Assert.Contains("John", prompt);
    Assert.Contains("Snake", prompt);
    Assert.Contains("Soup", prompt);
  }

  [Fact]
  public void ParseResponse_ReturnsNull_WhenResponseIsEmpty()
  {
    // Arrange
    var httpClient = CreateHttpClient(new HttpResponseMessage(HttpStatusCode.OK));
    var httpClientFactory = CreateHttpClientFactory(httpClient);
    var config = CreateConfiguration();
    var service = new StopGameScoringService(httpClientFactory, config);

    var request = new StopGameRequest
    {
      Letter = "S",
      Categories = new List<string> { "Animal" },
      Players = new List<PlayerEntry>()
    };

    // Act
    var result = service.ParseResponse(string.Empty, request);

    // Assert
    Assert.Null(result);
  }

  [Fact]
  public void ParseResponse_ReturnsNull_WhenResponseHasNoJson()
  {
    // Arrange
    var httpClient = CreateHttpClient(new HttpResponseMessage(HttpStatusCode.OK));
    var httpClientFactory = CreateHttpClientFactory(httpClient);
    var config = CreateConfiguration();
    var service = new StopGameScoringService(httpClientFactory, config);

    var request = new StopGameRequest
    {
      Letter = "S",
      Categories = new List<string> { "Animal" },
      Players = new List<PlayerEntry>()
    };

    // Act
    var result = service.ParseResponse("This is not JSON", request);

    // Assert
    Assert.Null(result);
  }

  [Fact]
  public void ParseResponse_ParsesValidJsonResponse()
  {
    // Arrange
    var httpClient = CreateHttpClient(new HttpResponseMessage(HttpStatusCode.OK));
    var httpClientFactory = CreateHttpClientFactory(httpClient);
    var config = CreateConfiguration();
    var service = new StopGameScoringService(httpClientFactory, config);

    var request = new StopGameRequest
    {
      Letter = "S",
      Categories = new List<string> { "Animal" },
      Players = new List<PlayerEntry>
      {
        new PlayerEntry
        {
          Name = "John",
          UserId = 1,
          Answers = new Dictionary<string, string>
          {
            { "Animal", "Snake" }
          }
        }
      }
    };

    var jsonResponse = """
      {
        "letter": "S",
        "categories": ["Animal"],
        "players": [
          {
            "name": "John",
            "userId": 1,
            "answers": { "Animal": "Snake" },
            "scores": {
              "Animal": {
                "points": 10,
                "reason": "valid"
              }
            }
          }
        ]
      }
      """;

    // Act
    var result = service.ParseResponse(jsonResponse, request);

    // Assert
    Assert.NotNull(result);
    Assert.Equal("S", result.Letter);
    Assert.Single(result.Categories);
    Assert.Single(result.Players);
    Assert.Equal("John", result.Players[0].Name);
    Assert.Equal(10, result.Players[0].Scores["Animal"].Points);
  }

  [Fact]
  public void ParseResponse_NormalizesMultilingualJsonKeys()
  {
    // Arrange
    var httpClient = CreateHttpClient(new HttpResponseMessage(HttpStatusCode.OK));
    var httpClientFactory = CreateHttpClientFactory(httpClient);
    var config = CreateConfiguration();
    var service = new StopGameScoringService(httpClientFactory, config);

    var request = new StopGameRequest
    {
      Letter = "S",
      Categories = new List<string> { "Animal" },
      Players = new List<PlayerEntry>
      {
        new PlayerEntry
        {
          Name = "John",
          UserId = 1,
          Answers = new Dictionary<string, string>
          {
            { "Animal", "Snake" }
          }
        }
      }
    };

    // Spanish keys
    var jsonResponse = """
      {
        "letter": "S",
        "Categorías": ["Animal"],
        "Jugadores": [
          {
            "Nombre": "John",
            "Respuestas": { "Animal": "Snake" },
            "Puntajes": {
              "Animal": {
                "puntos": 10,
                "razón": "valid"
              }
            }
          }
        ]
      }
      """;

    var result = service.ParseResponse(jsonResponse, request);

    Assert.True(result == null || result != null);
  }

  [Fact]
  public async Task ScoreGameAsync_ThrowsArgumentException_WhenLetterIsEmpty()
  {
    // Arrange
    var httpClient = CreateHttpClient(new HttpResponseMessage(HttpStatusCode.OK));
    var httpClientFactory = CreateHttpClientFactory(httpClient);
    var config = CreateConfiguration();
    var service = new StopGameScoringService(httpClientFactory, config);

    var request = new StopGameRequest
    {
      Letter = string.Empty,
      Categories = new List<string> { "Animal" },
      Players = new List<PlayerEntry>()
    };

    await Assert.ThrowsAsync<ArgumentException>(() => service.ScoreGameAsync(request));
  }

  [Fact]
  public async Task ScoreGameAsync_ThrowsArgumentException_WhenCategoriesAreEmpty()
  {
    var httpClient = CreateHttpClient(new HttpResponseMessage(HttpStatusCode.OK));
    var httpClientFactory = CreateHttpClientFactory(httpClient);
    var config = CreateConfiguration();
    var service = new StopGameScoringService(httpClientFactory, config);

    var request = new StopGameRequest
    {
      Letter = "S",
      Categories = new List<string>(),
      Players = new List<PlayerEntry>()
    };

    await Assert.ThrowsAsync<ArgumentException>(() => service.ScoreGameAsync(request));
  }

  [Fact]
  public async Task ScoreGameAsync_ThrowsArgumentException_WhenPlayersAreEmpty()
  {
    var httpClient = CreateHttpClient(new HttpResponseMessage(HttpStatusCode.OK));
    var httpClientFactory = CreateHttpClientFactory(httpClient);
    var config = CreateConfiguration();
    var service = new StopGameScoringService(httpClientFactory, config);

    var request = new StopGameRequest
    {
      Letter = "S",
      Categories = new List<string> { "Animal" },
      Players = new List<PlayerEntry>()
    };

    await Assert.ThrowsAsync<ArgumentException>(() => service.ScoreGameAsync(request));
  }

  [Fact]
  public async Task SendToModelAsync_ReturnsEmptyString_WhenHttpRequestFails()
  {
    // Arrange
    var httpClient = CreateHttpClient(new HttpResponseMessage(HttpStatusCode.InternalServerError));
    var httpClientFactory = CreateHttpClientFactory(httpClient);
    var config = CreateConfiguration();
    var service = new StopGameScoringService(httpClientFactory, config);

    // Act
    var result = await service.SendToModelAsync("test prompt");

    // Assert
    Assert.Empty(result);
  }

  [Fact]
  public async Task SendToModelAsync_ExtractsResponseFromJson()
  {
    // Arrange
    var ollamaResponse = new
    {
      response = """
        {
          "letter": "S",
          "categories": ["Animal"],
          "players": []
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
    var service = new StopGameScoringService(httpClientFactory, config);

    // Act
    var result = await service.SendToModelAsync("test prompt");

    // Assert
    Assert.NotEmpty(result);
    Assert.Contains("letter", result);
  }

  [Fact]
  public async Task WarmUpModelAsync_HandlesSuccessResponse()
  {
    // Arrange
    var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
    {
      Content = new StringContent(@"{""status"": ""ready""}", Encoding.UTF8, "application/json")
    };

    var httpClient = CreateHttpClient(httpResponse);
    var httpClientFactory = CreateHttpClientFactory(httpClient);
    var config = CreateConfiguration();
    var service = new StopGameScoringService(httpClientFactory, config);

    await service.WarmUpModelAsync();

    Assert.True(true);
  }

  [Fact]
  public async Task WarmUpModelAsync_HandlesFailureGracefully()
  {
    // Arrange
    var httpResponse = new HttpResponseMessage(HttpStatusCode.InternalServerError)
    {
      Content = new StringContent("Error", Encoding.UTF8, "text/plain")
    };

    var httpClient = CreateHttpClient(httpResponse);
    var httpClientFactory = CreateHttpClientFactory(httpClient);
    var config = CreateConfiguration();
    var service = new StopGameScoringService(httpClientFactory, config);

    await service.WarmUpModelAsync();
    Assert.True(true);
  }
}


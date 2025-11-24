using System.Net.WebSockets;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using WordRush.Core.Features.Game.CategoryTypes;
using WordRush.Core.Features.Realtime.Models;
using WordRush.Core.Features.Realtime.Models.CreateRoom;
using WordRush.Core.Features.Realtime.Models.GameSession;
using WordRush.Core.Features.Realtime.Models.JoinRoom;
using WordRush.Repository.Models;

namespace WordRush.Core.Features.Realtime.MessageHandler
{
  internal class GameRoomWebSocketMessageHandler : WebSocketMessageHandler
  {
    public override async Task HandleSocketMessage(WordRushWebSocketService webSocketService, WebSocket socket, string userID, string action, string jsonData)
    {
      if (!Enum.TryParse(action, out WebSocketMessageTypeEnums.GameRoomClientActions result))
      {
        Console.WriteLine("ERROR: Undefined action for Game cateogory: " + action);
        return;
      }

      switch (result)
      {
        case WebSocketMessageTypeEnums.GameRoomClientActions.CREATE:
          await CreateRoom(webSocketService, socket, userID, jsonData);
          break;
        case WebSocketMessageTypeEnums.GameRoomClientActions.JOIN:
          await JoinRoom(webSocketService, socket, userID, jsonData);
          break;
        case WebSocketMessageTypeEnums.GameRoomClientActions.LEAVE:
          await LeaveRoom(webSocketService, socket, userID);
          break;
        case WebSocketMessageTypeEnums.GameRoomClientActions.TOGGLE_READY:
          await ToggleReadyState(webSocketService, userID);
          break;
        case WebSocketMessageTypeEnums.GameRoomClientActions.REQUEST_DATA:
          await RequestRoomData(webSocketService, userID);
          break;
        case WebSocketMessageTypeEnums.GameRoomClientActions.START_GAME:
          await StartGame(webSocketService, userID);
          break;
        case WebSocketMessageTypeEnums.GameRoomClientActions.VALID_CATEGORY_CHECK:
          await CheckIfValidCategory(webSocketService, socket, jsonData);
          break;
        default:
          break;
      }
    }

    private async Task CreateRoom(WordRushWebSocketService webSocketService, WebSocket socket, string userID, string jsonData)
    {
      CreateGameRoomEvent createRoomEvent = JsonSerializer.Deserialize<CreateGameRoomEvent>(jsonData);

      GameRoom room = new(webSocketService.GenerateValidRoomID())
      {
        HostPlayerID = userID
      };

      webSocketService.UserToRoom[userID] = room.RoomId;
      webSocketService.Rooms[room.RoomId] = room;

      room.AddPlayer(userID, createRoomEvent.PlayerProfile, socket);

      CategoryType? categoryType;
      using (var scope = webSocketService.ServiceScopeFactory.CreateScope())
      {
        var categoryTypesService = scope.ServiceProvider.GetRequiredService<ICategoryTypes>();
        categoryType = await categoryTypesService.GetDefaultTypeWithColumns();

        if (categoryType != null && categoryType.CategoryColumns.Any())
        {
          categoryType = new CategoryType
          {
            Id = categoryType.Id,
            Name = categoryType.Name,
            CategoryColumns = categoryType.CategoryColumns.Select(col => new CategoryColumn
            {
              Id = col.Id,
              Column = col.Column
            }).ToList()
          };
        }
      }

      GameRoomCreatedEvent roomCreatedEventData = new GameRoomCreatedEvent(room.RoomId)
      {
        Settings = room.Settings,
        CategoryType = categoryType
      };

      // Cache categories in memory as well
      List<CategoryColumn> dbCategories = categoryType.CategoryColumns as List<CategoryColumn>;
      List<string> settingsCategories = new();
      for (int i = 0; i < dbCategories.Count; i++)
      {
        settingsCategories.Add(dbCategories[i].Column);
      }
      room.Settings.Categories = settingsCategories.ToArray();

      string messageCategory = WebSocketMessageTypeEnums.Categories.GAME_ROOM.ToString();
      string messageAction = WebSocketMessageTypeEnums.GameRoomServerActions.CREATED.ToString();

      WebSocketMessage message = new(messageCategory, messageAction, JsonSerializer.Serialize(roomCreatedEventData, webSocketService.JsonOptions));
      await webSocketService.SendAsync(socket, JsonSerializer.Serialize(message, webSocketService.JsonOptions));
    }

    private async Task JoinRoom(WordRushWebSocketService webSocketService, WebSocket socket, string userID, string jsonData)
    {
      JoinGameRoomEvent? joinGameRoomEvent = null;
      try
      {
        joinGameRoomEvent = JsonSerializer.Deserialize<JoinGameRoomEvent>(jsonData);
      }
      catch (Exception ex)
      {
        Log.Warning(ex, "[GAME ROOM] Failed to deserialize JoinGameRoomEvent. JSON Data: {JsonData}", jsonData);

        string messageCategory = WebSocketMessageTypeEnums.Categories.GAME_ROOM.ToString();
        string messageAction = WebSocketMessageTypeEnums.GameRoomServerActions.JOINED_NON_EXISTING_ROOM.ToString();
        WebSocketMessage message = new(messageCategory, messageAction, "{}");
        await webSocketService.SendAsync(socket, JsonSerializer.Serialize(message, webSocketService.JsonOptions));
        return;
      }

      if (joinGameRoomEvent == null || string.IsNullOrWhiteSpace(joinGameRoomEvent.RoomID))
      {
        string messageCategory = WebSocketMessageTypeEnums.Categories.GAME_ROOM.ToString();
        string messageAction = WebSocketMessageTypeEnums.GameRoomServerActions.JOINED_NON_EXISTING_ROOM.ToString();
        WebSocketMessage message = new(messageCategory, messageAction, "{}");
        await webSocketService.SendAsync(socket, JsonSerializer.Serialize(message, webSocketService.JsonOptions));
        return;
      }

      GameRoom? room = webSocketService.GetRoom(joinGameRoomEvent.RoomID);
      if (room != null)
      {
        webSocketService.UserToRoom[userID] = room.RoomId;

        room.AddPlayer(userID, joinGameRoomEvent.PlayerProfile, socket);

        await BroadcastRoomData(webSocketService, room);

        CategoryType? categoryType;
        using (var scope = webSocketService.ServiceScopeFactory.CreateScope())
        {
          var categoryTypesService = scope.ServiceProvider.GetRequiredService<ICategoryTypes>();
          categoryType = await categoryTypesService.GetDefaultTypeWithColumns();

          if (categoryType != null && categoryType.CategoryColumns.Any())
          {
            categoryType = new CategoryType
            {
              Id = categoryType.Id,
              Name = categoryType.Name,
              CategoryColumns = categoryType.CategoryColumns.Select(col => new CategoryColumn
              {
                Id = col.Id,
                Column = col.Column
              }).ToList()
            };
          }
        }

        GameRoomJoinedEvent roomJoinedEventData = new()
        {
          GameRoomID = room.RoomId,
          Settings = room.Settings,
          CategoryType = categoryType
        };

        string messageCategory = WebSocketMessageTypeEnums.Categories.GAME_ROOM.ToString();
        string messageAction = WebSocketMessageTypeEnums.GameRoomServerActions.JOINED.ToString();

        WebSocketMessage message = new(messageCategory, messageAction, JsonSerializer.Serialize(roomJoinedEventData, webSocketService.JsonOptions));
        await webSocketService.SendAsync(socket, JsonSerializer.Serialize(message, webSocketService.JsonOptions));
      }
      else
      {
        // The room doesn't exists, the user should be notified of this too (So an error message can be displayed)
        string messageCategory = WebSocketMessageTypeEnums.Categories.GAME_ROOM.ToString();
        string messageAction = WebSocketMessageTypeEnums.GameRoomServerActions.JOINED_NON_EXISTING_ROOM.ToString();

        WebSocketMessage message = new(messageCategory, messageAction, "{}");
        await webSocketService.SendAsync(socket, JsonSerializer.Serialize(message));
      }
    }

    private async Task LeaveRoom(WordRushWebSocketService webSocketService, WebSocket socket, string userID)
    {
      // Check if the user is in a room
      if (webSocketService.UserToRoom.TryGetValue(userID, out string roomID))
      {
        GameRoom? room = webSocketService.GetRoom(roomID);
        if (room != null)
        {
          await webSocketService.OnPlayerLeftRoom(socket, room, userID);
        }
      }
    }

    private async Task ToggleReadyState(WordRushWebSocketService webSocketService, string userID)
    {
      if (webSocketService.UserToRoom.TryGetValue(userID, out string roomID))
      {
        GameRoom? room = webSocketService.GetRoom(roomID);
        if (room != null)
        {
          room.ToggleReadyStateForPlayer(userID);
          await BroadcastRoomData(webSocketService, room);
        }
      }
    }

    private async Task RequestRoomData(WordRushWebSocketService webSocketService, string userID)
    {
      if (webSocketService.UserToRoom.TryGetValue(userID, out string roomID))
      {
        GameRoom? room = webSocketService.GetRoom(roomID);
        if (room != null)
        {
          await BroadcastRoomData(webSocketService, room);
        }
      }
    }

    private async Task StartGame(WordRushWebSocketService webSocketService, string userID)
    {
      if (webSocketService.UserToRoom.TryGetValue(userID, out string roomID))
      {
        GameRoom? room = webSocketService.GetRoom(roomID);
        if (room != null)
        {
          // Prepare game session
          room.PrepareGameSession();

          // Send message
          string messageCategory = WebSocketMessageTypeEnums.Categories.GAME_ROOM.ToString();
          string messageAction = WebSocketMessageTypeEnums.GameRoomServerActions.GAME_STARTED.ToString();

          WebSocketMessage message = new(messageCategory, messageAction, "{}");
          await webSocketService.BroadcastToRoomAsync(room.RoomId, JsonSerializer.Serialize(message));
        }
      }
    }

    private async Task CheckIfValidCategory(WordRushWebSocketService webSocketService, WebSocket socket, string jsonData)
    {
      CheckValidCategoryEvent checkValidCategoryEvent = JsonSerializer.Deserialize<CheckValidCategoryEvent>(jsonData);

      Console.WriteLine($"Checking if {checkValidCategoryEvent.Category} is valid");
      bool isValidCategory = true;  // TODO: Actually send the prompt and check if the category is valid

      CheckValidCategoryResultEvent resultJsonData = new();
      resultJsonData.IsValidCategory = isValidCategory;
      resultJsonData.Category = checkValidCategoryEvent.Category;

      // Send message
      string messageCategory = WebSocketMessageTypeEnums.Categories.GAME_ROOM.ToString();
      string messageAction = WebSocketMessageTypeEnums.GameRoomServerActions.VALID_CATEGORY_CHECK_RESULT.ToString();

      // Broadcast the new phase to all players in the room
      WebSocketMessage message = new(messageCategory, messageAction, JsonSerializer.Serialize(resultJsonData));
      await webSocketService.SendAsync(socket, JsonSerializer.Serialize(message));
    }

    /// <summary>
    /// Sends the updated room data to all the players in the specified room.
    /// </summary>
    /// <param name="webSocketService">WebSocketService instance used to seend the message.</param>
    /// <param name="room">The room to get the players from.</param>
    public static async Task BroadcastRoomData(WordRushWebSocketService webSocketService, GameRoom room)
    {
      RoomDataRequestedEvent roomData = room.GetRoomData();

      // Send message
      string messageCategory = WebSocketMessageTypeEnums.Categories.GAME_ROOM.ToString();
      string messageAction = WebSocketMessageTypeEnums.GameRoomServerActions.DATA_UPDATED.ToString();

      WebSocketMessage message = new(messageCategory, messageAction, JsonSerializer.Serialize(roomData, webSocketService.JsonOptions));
      await webSocketService.BroadcastToRoomAsync(room.RoomId, JsonSerializer.Serialize(message, webSocketService.JsonOptions));
    }
  }
}

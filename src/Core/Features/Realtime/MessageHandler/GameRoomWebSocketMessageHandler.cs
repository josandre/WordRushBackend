using System.Net.WebSockets;
using System.Text.Json;
using WordRush.Core.Features.Realtime.Models;
using WordRush.Core.Features.Realtime.Models.CreateRoom;
using WordRush.Core.Features.Realtime.Models.JoinRoom;

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
        default:
          break;
      }
    }

    private async Task CreateRoom(WordRushWebSocketService webSocketService, WebSocket socket, string userID, string jsonData)
    {
      CreateGameRoomEvent createRoomEvent = JsonSerializer.Deserialize<CreateGameRoomEvent>(jsonData);

      GameRoom room = new(webSocketService.GenerateValidRoomID())
      {
        // Register owner
        OwnerUserId = userID
      };

      webSocketService.UserToRoom[userID] = room.RoomId;
      webSocketService.Rooms[room.RoomId] = room;

      // Add the player who created the room
      room.AddParticipantSocket(socket);
      room.AddUser(userID, createRoomEvent.PlayerProfile);

      Console.WriteLine($"[GAME ROOM] The profile: Nickname: {createRoomEvent.PlayerProfile.Nickname}, Avatar: {createRoomEvent.PlayerProfile.Avatar}, Email: {createRoomEvent.PlayerProfile.Email}");
      Console.WriteLine($"[GAME ROOM] Room with ID: {room.RoomId} created by User {userID}");

      GameRoomCreatedEvent roomCreatedEventData = new GameRoomCreatedEvent(room.RoomId);

      // Send message
      string messageCategory = WebSocketMessageTypeEnums.Categories.GAME_ROOM.ToString();
      string messageAction = WebSocketMessageTypeEnums.GameRoomServerActions.CREATED.ToString();

      WebSocketMessage message = new(messageCategory, messageAction, JsonSerializer.Serialize(roomCreatedEventData));
      await webSocketService.SendAsync(socket, JsonSerializer.Serialize(message));
    }

    private async Task JoinRoom(WordRushWebSocketService webSocketService, WebSocket socket, string userID, string jsonData)
    {
      JoinGameRoomEvent joinGameRoomEvent = JsonSerializer.Deserialize<JoinGameRoomEvent>(jsonData);

      // The room exists
      if (webSocketService.Rooms.TryGetValue(joinGameRoomEvent.RoomID, out GameRoom room))
      {
        webSocketService.UserToRoom[userID] = room.RoomId;

        // Add the player to the room
        room.AddParticipantSocket(socket);
        room.AddUser(userID, joinGameRoomEvent.PlayerProfile);

        // Notify the users in the room about the new user
        await BroadcastRoomData(webSocketService, room);

        GameRoomJoinedEvent roomJoinedEventData = new();
        roomJoinedEventData.GameRoomID = room.RoomId;

        // Notify the user, so it can navigate to the lobby screen
        string messageCategory = WebSocketMessageTypeEnums.Categories.GAME_ROOM.ToString();
        string messageAction = WebSocketMessageTypeEnums.GameRoomServerActions.JOINED.ToString();

        WebSocketMessage message = new(messageCategory, messageAction, JsonSerializer.Serialize(roomJoinedEventData));
        await webSocketService.SendAsync(socket, JsonSerializer.Serialize(message));
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
      if (webSocketService.UserToRoom.TryGetValue(userID, out string roomID) &&
          webSocketService.Rooms.TryGetValue(roomID, out GameRoom room))
      {
        await webSocketService.OnPlayerLeftRoom(socket, room, userID);
      }
    }

    private async Task ToggleReadyState(WordRushWebSocketService webSocketService, string userID)
    {
      if (webSocketService.UserToRoom.TryGetValue(userID, out string roomID) &&
          webSocketService.Rooms.TryGetValue(roomID, out GameRoom room))
      {
        room.ToggleReady(userID);
        await BroadcastRoomData(webSocketService, room);
      }
    }

    private async Task RequestRoomData(WordRushWebSocketService webSocketService, string userID)
    {
      if (webSocketService.UserToRoom.TryGetValue(userID, out string roomID) &&
          webSocketService.Rooms.TryGetValue(roomID, out GameRoom room))
      {
        await BroadcastRoomData(webSocketService, room);
      }
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

      WebSocketMessage message = new(messageCategory, messageAction, JsonSerializer.Serialize(roomData));
      await webSocketService.BroadcastToRoomAsync(room.RoomId, JsonSerializer.Serialize(message));
    }
  }
}

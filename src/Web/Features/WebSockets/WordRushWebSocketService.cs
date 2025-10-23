using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using WordRush.Core.Features;

namespace WordRush.Web.Features.WebSockets
{
  public sealed class WordRushWebSocketService : IWordRushWebSocketService
  {
    private static readonly ConcurrentDictionary<string, GameRoom> GameRooms = new();
    private static readonly ConcurrentDictionary<WebSocket, string> UserRoomMap = new();
    private static readonly ConcurrentDictionary<WebSocket, string> UserIdMap = new();
    private static readonly ConcurrentDictionary<WebSocket, string> UserNameMap = new();

    public async Task HandleConnectionAsync(WebSocket webSocket)
    {
      string userId = Guid.NewGuid().ToString()[..6];
      UserIdMap[webSocket] = userId;
      UserNameMap[webSocket] = $"Player_{userId}";

      await SendAsync(webSocket, $"Welcome! Your User ID is {userId}");      

      byte[] buffer = new byte[1024 * 4];

      try
      {
        while (webSocket.State == WebSocketState.Open)
        {
          WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
          if (result.MessageType == WebSocketMessageType.Close)
            break;

          string message = Encoding.UTF8.GetString(buffer, 0, result.Count).Trim();
          await HandleMessageAsync(webSocket, message);
        }
      }
      finally
      {
        await HandleDisconnectAsync(webSocket);
      }
    }

    private async Task HandleMessageAsync(WebSocket webSocket, string message)
    {
      string userId = UserIdMap.GetValueOrDefault(webSocket, "Unknown");
      string userName = UserNameMap.GetValueOrDefault(webSocket, userId);

      if (message.Equals("CREATE_GAMEROOM", StringComparison.OrdinalIgnoreCase))
      {
        // Leave old room first (and broadcast that update)
        GameRoom? oldRoom = null;
        if (UserRoomMap.TryGetValue(webSocket, out string? oldRoomId) &&
            GameRooms.TryGetValue(oldRoomId, out oldRoom))
        {
          await LeaveCurrentGameRoomAsync(webSocket);
          if (oldRoom != null)
            await BroadcastUserListAsync(oldRoom);
        }

        // Create new room and join it
        var gameRoom = new GameRoom { OwnerUserId = userId };
        GameRooms[gameRoom.RoomId] = gameRoom;

        gameRoom.Add(webSocket);
        UserRoomMap[webSocket] = gameRoom.RoomId;

        await SendAsync(webSocket, $"Game room created. Room ID: {gameRoom.RoomId}");
        await SendAsync(webSocket, $"You are now the owner of game room {gameRoom.RoomId}");

        // Broadcast the updated user list to the new room
        await BroadcastUserListAsync(gameRoom);
      }

      else if (message.StartsWith("JOIN_GAMEROOM:", StringComparison.OrdinalIgnoreCase))
      {
        string roomId = message.Split(':').Last();
        if (GameRooms.TryGetValue(roomId, out GameRoom? gameRoom))
        {
          await LeaveCurrentGameRoomAsync(webSocket);
          gameRoom.Add(webSocket);
          UserRoomMap[webSocket] = roomId;

          await SendAsync(webSocket, $"Joined game room: {roomId}");
          await BroadcastToGameRoom(gameRoom, $"User {userName} joined the game room.", webSocket);
          await BroadcastUserListAsync(gameRoom);
        }
        else
        {
          await SendAsync(webSocket, $"Game room {roomId} not found.");
        }
      }
      else if (message.Equals("LIST_USERS", StringComparison.OrdinalIgnoreCase))
      {
        if (UserRoomMap.TryGetValue(webSocket, out string? roomId) &&
            GameRooms.TryGetValue(roomId, out GameRoom? gameRoom))
        {
          var users = gameRoom.GetUserIds(UserNameMap);
          await SendAsync(webSocket, $"Users in game room ({users.Count}): {string.Join(", ", users)}");
        }
        else
        {
          await SendAsync(webSocket, "You are not in a game room.");
        }
      }
      else if (message.StartsWith("CHANGE_NAME:", StringComparison.OrdinalIgnoreCase))
      {
        string newName = message.Split(':', 2).Last().Trim();
        if (!string.IsNullOrEmpty(newName))
        {
          UserNameMap[webSocket] = newName;
          await SendAsync(webSocket, $"Your name has been changed to {newName}");
          if (UserRoomMap.TryGetValue(webSocket, out string? roomId) &&
              GameRooms.TryGetValue(roomId, out GameRoom? gameRoom))
          {
            await BroadcastUserListAsync(gameRoom);
          }
        }
      }
      else if (message.Equals("CLOSE_GAMEROOM", StringComparison.OrdinalIgnoreCase))
      {
        if (UserRoomMap.TryGetValue(webSocket, out string? roomId) &&
            GameRooms.TryGetValue(roomId, out GameRoom? gameRoom))
        {
          if (gameRoom.OwnerUserId == userId)
          {
            // Notify everyone that the room is closing
            await BroadcastToGameRoom(gameRoom, "ROOM_CLOSED");
            await BroadcastToGameRoom(gameRoom, "Game room has been closed by the owner.");

            // Remove all users from the room
            foreach (var socket in gameRoom.Participants.ToList())
            {
              UserRoomMap.TryRemove(socket, out _);
              if (socket != webSocket && socket.State == WebSocketState.Open)
              {
                await SendAsync(socket, "You have been removed because the room was closed.");
              }
            }

            // Delete room
            GameRooms.TryRemove(roomId, out _);
            gameRoom.Participants.Clear();

            await SendAsync(webSocket, "You closed your game room.");
          }
          else
          {
            await SendAsync(webSocket, "Only the room owner can close this game room.");
          }
        }
      }

      else
      {
        if (UserRoomMap.TryGetValue(webSocket, out string? roomId) &&
            GameRooms.TryGetValue(roomId, out GameRoom? gameRoom))
        {
          await BroadcastToGameRoom(gameRoom, $"[{userName}] {message}", webSocket);
        }
        else
        {
          await SendAsync(webSocket, "You are not in a game room. Use CREATE_GAMEROOM or JOIN_GAMEROOM:<id> first.");
        }
      }
    }

    private async Task LeaveCurrentGameRoomAsync(WebSocket webSocket)
    {
      if (UserRoomMap.TryRemove(webSocket, out string? oldRoomId) &&
          GameRooms.TryGetValue(oldRoomId, out GameRoom? oldGameRoom))
      {
        string userName = UserNameMap.GetValueOrDefault(webSocket, "Unknown");
        oldGameRoom.Remove(webSocket);
        await BroadcastToGameRoom(oldGameRoom, $"User {userName} left the game room.", webSocket);
        await BroadcastUserListAsync(oldGameRoom);

        if (oldGameRoom.IsEmpty)
        {
          GameRooms.TryRemove(oldRoomId, out _);
        }
      }
    }

    private async Task HandleDisconnectAsync(WebSocket webSocket)
    {
      string? roomId = null;
      GameRoom? gameRoom = null;

      if (UserRoomMap.TryGetValue(webSocket, out roomId) &&
          GameRooms.TryGetValue(roomId, out gameRoom))
      {
        string userId = UserIdMap.GetValueOrDefault(webSocket, "Unknown");

        // If the disconnected user is the room owner, close the room for everyone
        if (gameRoom.OwnerUserId == userId)
        {
          await BroadcastToGameRoom(gameRoom, "ROOM_CLOSED");
          await BroadcastToGameRoom(gameRoom, "Game room has been closed because the owner disconnected.");

          foreach (var socket in gameRoom.Participants.ToList())
          {
            UserRoomMap.TryRemove(socket, out _);
            if (socket != webSocket && socket.State == WebSocketState.Open)
            {
              await SendAsync(socket, "You have been removed because the room owner disconnected.");
            }
          }

          GameRooms.TryRemove(roomId, out _);
          gameRoom.Participants.Clear();
        }
        else
        {
          // Non-owner user just leaves normally
          await LeaveCurrentGameRoomAsync(webSocket);
        }
      }

      // Remove user tracking
      UserIdMap.TryRemove(webSocket, out _);
      UserNameMap.TryRemove(webSocket, out _);

      // Close the socket if still open
      if (webSocket.State == WebSocketState.Open)
      {
        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnected", CancellationToken.None);
      }
    }


    private static async Task SendAsync(WebSocket socket, string message)
    {
      byte[] bytes = Encoding.UTF8.GetBytes(message);
      await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private static async Task BroadcastToGameRoom(GameRoom gameRoom, string message, WebSocket? exclude = null)
    {
      byte[] bytes = Encoding.UTF8.GetBytes(message);
      List<WebSocket> participants;

      lock (gameRoom.Participants)
      {
        participants = gameRoom.Participants.ToList();
      }

      foreach (var socket in participants)
      {
        if (socket.State == WebSocketState.Open && socket != exclude)
        {
          await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }
      }
    }

    private static async Task BroadcastUserListAsync(GameRoom gameRoom)
    {
      var users = gameRoom.GetUserIds(UserNameMap);
      string listMessage = "USER_LIST:" + string.Join(",", users);
      await BroadcastToGameRoom(gameRoom, listMessage);
    }
  }
}

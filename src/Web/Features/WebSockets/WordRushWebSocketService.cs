using System.Net.WebSockets;
using System.Text;
using System.Collections.Concurrent;
using System.Text.Json;
using WordRush.Core.Features;

namespace WordRush.Web.Features.WebSockets
{
  public class WordRushWebSocketService: IWordRushWebSocketService
  {
    private readonly ConcurrentDictionary<string, GameRoom> _rooms = new();
    private readonly ConcurrentDictionary<WebSocket, string> _socketToUser = new();
    private readonly ConcurrentDictionary<string, string> _userToRoom = new();
    public async Task BroadcastAsync(string roomId, string message)
    {
      if (!_rooms.TryGetValue(roomId, out var room))
        return;

      var bytes = Encoding.UTF8.GetBytes(message);
      var segment = new ArraySegment<byte>(bytes);

      lock (room.Participants)
      {
        foreach (var socket in room.Participants)
        {
          if (socket.State == WebSocketState.Open)
            _ = socket.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
        }
      }
    }

    public async Task CloseRoomAsync(string roomId)
    {
      if (!_rooms.TryRemove(roomId, out var room))
        return;

      var message = Encoding.UTF8.GetBytes("Room closed by owner.");
      var segment = new ArraySegment<byte>(message);

      lock (room.Participants)
      {
        foreach (var socket in room.Participants)
        {
          if (socket.State == WebSocketState.Open)
            _ = socket.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
        }
      }

      // Disconnect participants
      foreach (var socket in room.Participants)
      {
        if (socket.State == WebSocketState.Open)
          await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Room closed", CancellationToken.None);
      }
    }

    public async Task<IList<string>> GetUsersInRoomAsync(string roomId)
    {
      if (!_rooms.TryGetValue(roomId, out var room))
        return new List<string>();

      return room.GetUserIds(_socketToUser);
    }

    public async Task SendToUserAsync(string userId, string message)
    {
      var target = _socketToUser.FirstOrDefault(p => p.Value == userId).Key;
      if (target == null || target.State != WebSocketState.Open)
        return;

      var bytes = Encoding.UTF8.GetBytes(message);
      await target.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
    }
    public async Task HandleConnectionAsync(WebSocket socket)
    {
      var userId = Guid.NewGuid().ToString();
      _socketToUser[socket] = userId;

      await SendAsync(socket, $"Connected! Your User ID is {userId}");

      try
      {
        var buffer = new byte[1024 * 4];
        while (socket.State == WebSocketState.Open)
        {
          var result = await socket.ReceiveAsync(buffer, CancellationToken.None);
          if (result.MessageType == WebSocketMessageType.Close)
            break;

          var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
          await ProcessMessageAsync(socket, userId, message);
        }
      }
      finally
      {
        await RemoveUserFromRoom(socket);
      }
    }

    private async Task ProcessMessageAsync(WebSocket socket, string userId, string message)
    {
      if (message.StartsWith("CREATE_GAMEROOM", StringComparison.OrdinalIgnoreCase))
      {
        var room = new GameRoom { OwnerUserId = userId };
        room.Add(socket);
        _rooms[room.RoomId] = room;
        _userToRoom[userId] = room.RoomId;

        await SendAsync(socket, $"Room created! Room ID: {room.RoomId}");
        await BroadcastUserList(room);
        return;
      }

      if (message.StartsWith("JOIN_GAMEROOM:", StringComparison.OrdinalIgnoreCase))
      {
        var roomId = message.Split(':')[1];
        if (_rooms.TryGetValue(roomId, out var room))
        {
          room.Add(socket);
          _userToRoom[userId] = roomId;
          await SendAsync(socket, $"Joined room {roomId}");
          await BroadcastUserList(room);
        }
        else
        {
          await SendAsync(socket, $"Room {roomId} not found");
        }
        return;
      }

      if (message.StartsWith("UPDATE_PROFILE:", StringComparison.OrdinalIgnoreCase))
      {
        var json = message.Substring("UPDATE_PROFILE:".Length);
        try
        {
          var profile = JsonSerializer.Deserialize<UserProfile>(json);
          if (profile != null && _userToRoom.TryGetValue(userId, out var roomId) && _rooms.TryGetValue(roomId, out var room))
          {
            room.Profiles[userId] = profile;
            await BroadcastUserList(room);
          }
        }
        catch (Exception ex)
        {
          await SendAsync(socket, $"Profile error: {ex.Message}");
        }
        return;
      }

      if (message.Equals("TOGGLE_READY", StringComparison.OrdinalIgnoreCase))
      {
        if (_userToRoom.TryGetValue(userId, out var roomId) && _rooms.TryGetValue(roomId, out var room))
        {
          room.ToggleReady(userId);
          await BroadcastUserList(room);
        }
        return;
      }

      if (message.Equals("START_GAME", StringComparison.OrdinalIgnoreCase))
      {
        if (_userToRoom.TryGetValue(userId, out var roomId) && _rooms.TryGetValue(roomId, out var room))
        {
          if (userId != room.OwnerUserId)
          {
            await SendAsync(socket, "Only the host can start the game.");
            return;
          }

          if (!room.AllReady)
          {
            await SendAsync(socket, "Not all players are ready.");
            return;
          }

          await BroadcastToRoom(room, "GAME_STARTING");
        }
        return;
      }

      if (message.Equals("CLOSE_GAMEROOM", StringComparison.OrdinalIgnoreCase))
      {
        if (_userToRoom.TryGetValue(userId, out var roomId) && _rooms.TryGetValue(roomId, out var room))
        {
          if (room.OwnerUserId != userId)
          {
            await SendAsync(socket, "Only the host can close this room.");
            return;
          }

          await BroadcastToRoom(room, "ROOM_CLOSED");
          foreach (var s in room.Participants)
            await s.CloseAsync(WebSocketCloseStatus.NormalClosure, "Room closed", CancellationToken.None);

          _rooms.TryRemove(roomId, out _);
        }
        return;
      }

      // Default: Broadcast message to all players in room
      if (_userToRoom.TryGetValue(userId, out var roomIdDefault) && _rooms.TryGetValue(roomIdDefault, out var defaultRoom))
      {
        await BroadcastToRoom(defaultRoom, $"{userId}: {message}");
      }
    }

    private async Task BroadcastUserList(GameRoom room)
    {
      var json = JsonSerializer.Serialize(room.GetPlayerSnapshots());
      await BroadcastToRoom(room, $"USER_LIST_JSON:{json}");
    }

    private async Task BroadcastToRoom(GameRoom room, string message)
    {
      var bytes = Encoding.UTF8.GetBytes(message);
      var segment = new ArraySegment<byte>(bytes);

      lock (room.Participants)
      {
        foreach (var s in room.Participants.ToList())
        {
          if (s.State == WebSocketState.Open)
            _ = s.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
        }
      }
    }

    private async Task SendAsync(WebSocket socket, string message)
    {
      if (socket.State != WebSocketState.Open) return;
      var bytes = Encoding.UTF8.GetBytes(message);
      await socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private async Task RemoveUserFromRoom(WebSocket socket)
    {
      if (!_socketToUser.TryRemove(socket, out var userId))
        return;

      if (_userToRoom.TryRemove(userId, out var roomId) && _rooms.TryGetValue(roomId, out var room))
      {
        // If the user leaving is the room owner, close the entire room
        if (room.OwnerUserId == userId)
        {
          await BroadcastToRoom(room, "ROOM_CLOSED_BY_OWNER");

          foreach (var s in room.Participants.ToList())
          {
            if (s.State == WebSocketState.Open)
              await s.CloseAsync(WebSocketCloseStatus.NormalClosure, "Room closed by owner", CancellationToken.None);
          }

          _rooms.TryRemove(roomId, out _);
          return;
        }

        // Regular user leaves
        room.Remove(socket);
        room.ReadyStatus.TryRemove(userId, out _);
        room.Profiles.TryRemove(userId, out _);

        if (room.IsEmpty)
        {
          _rooms.TryRemove(roomId, out _);
        }
        else
        {
          await BroadcastUserList(room);
        }
      }
    }

  }
}

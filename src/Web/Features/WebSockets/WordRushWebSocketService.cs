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
      // Assign or reuse a user ID for this socket
      string userId;

      if (!_socketToUser.TryGetValue(socket, out userId))
      {
        userId = Guid.NewGuid().ToString();
        _socketToUser[socket] = userId;

        Console.WriteLine($"[CONNECTED] New client assigned userId = {userId}");
      }

      var buffer = new byte[1024 * 4];

      try
      {
        // Listen for incoming messages
        while (socket.State == WebSocketState.Open)
        {
          var result = await socket.ReceiveAsync(
              new ArraySegment<byte>(buffer),
              CancellationToken.None
          );

          // Handle client disconnects
          if (result.MessageType == WebSocketMessageType.Close)
          {
            await HandleDisconnectAsync(socket, userId);
            break;
          }

          // Process incoming text message
          var message = Encoding.UTF8
              .GetString(buffer, 0, result.Count)
              .Trim();

          await ProcessMessageAsync(socket, userId, message);
        }
      }
      catch (Exception ex)
      {
        Console.WriteLine($"[ERROR] Connection failed: {ex.Message}");
        await CloseSocketSafely(socket);
      }
    }

    private async Task ProcessMessageAsync(WebSocket socket, string userId, string message)
    {
      if (string.IsNullOrWhiteSpace(message))
        return;

      // CREATE ROOM
      if (message == "CREATE_GAMEROOM")
      {
        var room = new GameRoom();

        // Register owner
        room.OwnerUserId = userId;
        _userToRoom[userId] = room.RoomId;
        _rooms[room.RoomId] = room;
        room.Add(socket);

        // ✅ Send standardized message
        await SendAsync(socket, $"ROOM_CREATED:{room.RoomId}");

        Console.WriteLine($"[ROOM_CREATED] User {userId} created room {room.RoomId}");

        // Broadcast updated list (just the owner for now)
        await BroadcastUserList(room);
        return;
      }

      // JOIN ROOM
      if (message.StartsWith("JOIN_GAMEROOM:"))
      {
        var roomId = message.Replace("JOIN_GAMEROOM:", "").Trim();

        if (_rooms.TryGetValue(roomId, out var room))
        {
          if (room.GetUserIds(_socketToUser).Contains(userId))
          {
            await SendAsync(socket, "ALREADY_IN_ROOM");
            return;
          }

          room.Add(socket);
          _userToRoom[userId] = roomId;

          await SendAsync(socket, $"JOINED_ROOM:{roomId}");
          await SendAsync(socket, "REQUEST_PROFILE_UPDATE");

          Console.WriteLine($"[JOINED_ROOM] User {userId} joined {roomId}");
          await BroadcastUserList(room);
        }
        else
        {
          await SendAsync(socket, $"ROOM_NOT_FOUND:{roomId}");
        }
        return;
      }

      // UPDATE PROFILE
      if (message.StartsWith("UPDATE_PROFILE:"))
      {
        var json = message.Replace("UPDATE_PROFILE:", "").Trim();
        try
        {
          var profile = JsonSerializer.Deserialize<UserProfile>(json);
          if (profile == null)
          {
            await SendAsync(socket, "INVALID_PROFILE_DATA");
            return;
          }

          if (_userToRoom.TryGetValue(userId, out var roomId) &&
              _rooms.TryGetValue(roomId, out var room))
          {
            room.Profiles[userId] = profile;
            room.ReadyStatus[userId] = false;

            Console.WriteLine($"[PROFILE_UPDATE] {userId}: {profile.Nickname} ({profile.Avatar})");
            await BroadcastUserList(room);
          }
          else
          {
            await SendAsync(socket, "PROFILE_UPDATE_FAILED");
          }
        }
        catch (Exception ex)
        {
          Console.WriteLine($"[ERROR] Failed to parse/update profile: {ex.Message}");
          await SendAsync(socket, $"PROFILE_ERROR:{ex.Message}");
        }
        return;
      }

      // TOGGLE READY
      if (message == "TOGGLE_READY")
      {
        if (_userToRoom.TryGetValue(userId, out var roomId) &&
            _rooms.TryGetValue(roomId, out var room))
        {
          room.ToggleReady(userId);
          await BroadcastUserList(room);
        }
        return;
      }

      // START GAME
      if (message == "START_GAME")
      {
        if (_userToRoom.TryGetValue(userId, out var roomId) &&
            _rooms.TryGetValue(roomId, out var room))
        {
          if (room.OwnerUserId == userId)
          {
            Console.WriteLine($"[GAME_STARTING] Room {roomId}");
            await BroadcastToRoom(room, "GAME_STARTING");
          }
          else
          {
            await SendAsync(socket, "HOST_ONLY_START");
          }
        }
        return;
      }
      // LEAVE ROOM
      if (message == "LEAVE_ROOM")
      {
        Console.WriteLine($"[LEAVE_ROOM] {userId} requested to leave the room.");

        await RemoveUserFromRoom(socket);
        await SendAsync(socket, "LEFT_ROOM");

        return;
      }

      // CLOSE ROOM
      if (message == "CLOSE_GAMEROOM")
      {
        if (_userToRoom.TryGetValue(userId, out var roomId) &&
            _rooms.TryGetValue(roomId, out var room))
        {
          if (room.OwnerUserId == userId)
          {
            Console.WriteLine($"[ROOM_CLOSED] {roomId}");
            await BroadcastToRoom(room, "ROOM_CLOSED_BY_OWNER");
            _rooms.TryRemove(roomId, out _);
          }
        }
        return;
      }

      // UNHANDLED
      Console.WriteLine($"[UNHANDLED MESSAGE] {message}");
    }

    private async Task BroadcastUserList(GameRoom room)
    {
      var users = room.GetPlayerSnapshots();
      var json = JsonSerializer.Serialize(users);

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
      Console.WriteLine($"[REMOVE_USER] Cleaning up {userId} from room...");

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

    private async Task HandleDisconnectAsync(WebSocket socket, string userId)
    {
      Console.WriteLine($"[DISCONNECTED] userId = {userId}");

      try
      {
        // Gracefully close if still open
        if (socket.State == WebSocketState.Open)
        {
          await socket.CloseAsync(
            WebSocketCloseStatus.NormalClosure,
            "Client disconnected",
            CancellationToken.None
          );
        }
      }
      catch (Exception ex)
      {
        Console.WriteLine($"[WARN] Error closing socket for {userId}: {ex.Message}");
      }

      // ✅ Unified cleanup path
      await RemoveUserFromRoom(socket);

      // ✅ Log final cleanup confirmation
      Console.WriteLine($"[CLEANUP] User {userId} fully removed from system.");
    }

    private async Task CloseSocketSafely(WebSocket socket)
    {
      try
      {
        if (socket.State != WebSocketState.Closed &&
            socket.State != WebSocketState.Aborted)
        {
          await socket.CloseAsync(
              WebSocketCloseStatus.InternalServerError,
              "Internal server error",
              CancellationToken.None
          );
        }
      }
      catch { /* ignore cleanup exceptions */ }
    }

  }
}

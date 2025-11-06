using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using LaunchDarkly.Logging;
using Serilog;
using WordRush.Core.Features.Realtime.MessageHandler;

namespace WordRush.Core.Features.Realtime
{
  public class WordRushWebSocketService : IWordRushWebSocketService
  {
    // WebSocket message handlers
    // Useful for delegating the logic to handle specific messages and logic
    private bool initializedMessageHandlers = false;
    private readonly ConcurrentDictionary<string, WebSocketMessageHandler> messageHandlers = new();

    private readonly JsonSerializerOptions options = new()
    {
      UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Skip
    };

    public ConcurrentDictionary<string, GameRoom> Rooms { get; } = new();

    public ConcurrentDictionary<WebSocket, string> SocketToUser { get; } = new();

    public ConcurrentDictionary<string, string> UserToRoom { get; } = new();

    public GameRoom? GetRoom(string roomId)
    {
      return Rooms.TryGetValue(roomId, out GameRoom? room) ? room : null;
    }

    /// <summary>
    /// Checks all the existing rooms and generates a unique code for a new room.
    /// </summary>
    /// <returns>A unique identifier for a new GameRoom.</returns>
    public string GenerateValidRoomID()
    {
      StringBuilder id = new();

      Random random = new();
      do
      {
        _ = id.Clear();
        for (int i = 0; i < 5; i++)
        {
          _ = id.Append(random.Next(10));
        }
      }
      while (Rooms.TryGetValue(id.ToString(), out _));

      return id.ToString();
    }

    /// <summary>
    /// Handles a new WebSocket connection and message loop.
    /// </summary>
    /// <param name="socket">The websocket of the specified connection.</param>
    public async Task HandleConnectionAsync(WebSocket socket)
    {
      // Assign or reuse a user ID for this socket
      if (!SocketToUser.TryGetValue(socket, out string userId))
      {
        userId = Guid.NewGuid().ToString();
        SocketToUser[socket] = userId;

        Log.Warning($"[CONNECTED] New client assigned userId = {userId}");
      }

      byte[] buffer = new byte[1024 * 4];

      try
      {
        // Listen for incoming messages
        while (socket.State == WebSocketState.Open)
        {
          WebSocketReceiveResult result = await socket.ReceiveAsync(
              new ArraySegment<byte>(buffer),
              CancellationToken.None);

          // Handle client disconnects
          if (result.MessageType == WebSocketMessageType.Close)
          {
            await HandleDisconnectAsync(socket, userId);
            break;
          }

          // Process incoming text message
          string message = Encoding.UTF8
              .GetString(buffer, 0, result.Count)
              .Trim();

          await ProcessMessageAsync(socket, userId, message);
        }
      }
      catch (Exception ex)
      {
        Log.Warning($"[ERROR] Connection failed: {ex.Message}");
        await CloseSocketSafely(socket);
      }
    }

    /// <summary>
    /// Sends a message to a single web socket.
    /// </summary>
    /// <param name="socket">The target that will receive the message.</param>
    /// <param name="message">The message itself.</param>
    public async Task SendAsync(WebSocket socket, string message)
    {
      if (socket.State != WebSocketState.Open)
      {
        return;
      }

      byte[] bytes = Encoding.UTF8.GetBytes(message);
      ArraySegment<byte> segment = new(bytes);

      await socket.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
    }

    /// <summary>
    /// Sends a message to all the users in a room.
    /// </summary>
    /// <param name="roomId">The room identifier.</param>
    /// <param name="message">The message itself.</param>
    public async Task BroadcastToRoomAsync(string roomId, string message)
    {
      GameRoom? room = GetRoom(roomId);
      if (room != null)
      {
        await BroadcastToRoomAsync(room, message);
      }
    }

    /// <summary>
    /// Sends a message to all the users in a room.
    /// </summary>
    /// <param name="room">The room reference.</param>
    /// <param name="message">The message itself.</param>
    public async Task BroadcastToRoomAsync(GameRoom room, string message)
    {
      byte[] bytes = Encoding.UTF8.GetBytes(message);
      ArraySegment<byte> segment = new(bytes);

      List<WebSocket> sockets = new();

      lock (room.PlayerSockets)
      {
        foreach (WebSocket socket in room.PlayerSockets)
        {
          // TODO: Clear invalid sockets?
          if (socket.State == WebSocketState.Open)
          {
            sockets.Add(socket);
          }
        }
      }

      foreach (WebSocket socket in sockets)
      {
        await socket.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
      }
    }

    /// <summary>
    /// Called when a player leaves a room in any context.
    /// </summary>
    /// <param name="socket">The user associated socket.</param>
    /// <param name="room">The room where is participating.</param>
    /// <param name="userID">The userID</param>
    public async Task OnPlayerLeftRoom(WebSocket socket, GameRoom room, string userID)
    {
      _ = UserToRoom.TryRemove(userID, out _);
      room.RemovePlayer(userID, socket);

      // Close the room for everyone if the owner leaves
      if (userID == room.HostPlayerID)
      {
        // Send message, so everyone leaves the room
        string messageCategory = WebSocketMessageTypeEnums.Categories.GAME_ROOM.ToString();
        string messageAction = WebSocketMessageTypeEnums.GameRoomServerActions.CLOSED.ToString();

        WebSocketMessage message = new(messageCategory, messageAction, "{}");
        await BroadcastToRoomAsync(room.RoomId, JsonSerializer.Serialize(message));

        // Destroy the room
        _ = Rooms.TryRemove(room.RoomId, out _);
      }
      else
      {
        // Refresh the room for everyone, doesn't matter if a non owner user leaves the room
        await GameRoomWebSocketMessageHandler.BroadcastRoomData(this, room);
      }
    }

    /// <summary>
    /// Splits the received message and deserializes it, every message consist in a type {Category|Action} and a JSON string.
    /// </summary>
    /// <param name="socket">The socket used in the connection.</param>
    /// <param name="userId">The userID associated to the socket.</param>
    /// <param name="message">The received message.</param>
    private async Task ProcessMessageAsync(WebSocket socket, string userId, string message)
    {
      if (string.IsNullOrWhiteSpace(message))
      {
        return;
      }

      if (!initializedMessageHandlers)
      {
        _ = messageHandlers.TryAdd(WebSocketMessageTypeEnums.Categories.GAME_ROOM.ToString(), new GameRoomWebSocketMessageHandler());
        _ = messageHandlers.TryAdd(WebSocketMessageTypeEnums.Categories.GAME_SESSION.ToString(), new GameSessionWebSocketMessageHandler());

        initializedMessageHandlers = true;
      }

      // First, try to deserealize the received message
      try
      {
        Log.Warning(message);
        WebSocketMessage webSocketMessage = JsonSerializer.Deserialize<WebSocketMessage>(message, options);
        Log.Warning($"Deserialized: type: {webSocketMessage.Type}, data: {webSocketMessage.JsonData}");

        // Then handle the message based on its type
        string[] typeParts = webSocketMessage.Type.Split("|");
        if (typeParts.Length == 2)
        {
          string type = typeParts[0];
          string action = typeParts[1];

          if (messageHandlers.TryGetValue(type, out WebSocketMessageHandler messageHandler))
          {
            await messageHandler.HandleSocketMessage(this, socket, userId, action, webSocketMessage.JsonData);
          }
          else
          {
            Log.Error($"Undefined handler for the category {type} | {action}");
          }
        }
        else
        {
          Log.Warning($"Error: Incorrect format for WebSocket message type: {webSocketMessage.Type}");
        }
      }
      catch (Exception e)
      {
        Log.Warning("ERROR: Couldn't parse the JSON from the WebSocket message." + e.Message);
      }
    }

    private async Task HandleDisconnectAsync(WebSocket socket, string userId)
    {
      Log.Warning($"[DISCONNECTED] userId = {userId}");

      try
      {
        // Successfully close if still open
        if (socket.State == WebSocketState.Open)
        {
          await socket.CloseAsync(
            WebSocketCloseStatus.NormalClosure,
            "Client disconnected",
            CancellationToken.None);
        }
      }
      catch (Exception ex)
      {
        Log.Warning($"[WARN] Error closing socket for {userId}: {ex.Message}");
      }

      // If the player is in a room, remove it from there
      if (UserToRoom.TryGetValue(userId, out string roomID))
      {
        GameRoom? room = GetRoom(roomID);
        if (room != null)
        {
          await OnPlayerLeftRoom(socket, room, userId);
        }
      }

      Log.Warning($"[CLEANUP] User {userId} fully removed from system.");
    }

    private async Task CloseSocketSafely(WebSocket socket)
    {
      if (socket == null)
      {
        Log.Warning("[CloseSocketSafely] Attempted to close a null WebSocket instance.");
        return;
      }

      try
      {
        if (socket.State is not WebSocketState.Closed and not WebSocketState.Aborted)
        {
          await socket.CloseAsync(
              WebSocketCloseStatus.InternalServerError,
              "Internal server error",
              CancellationToken.None);
          Log.Information("[CloseSocketSafely] Closed WebSocket sucessfully with InternalServerError status.");
        }
      }
      catch (WebSocketException ex)
      {
        Log.Warning(ex, "[CloseSocketSafely] WebSocketException occurred while closing socket. It may already be closed or aborted.");
      }
      catch (ObjectDisposedException)
      {
        Log.Debug("[CloseSocketSafely] WebSocket already disposed — ignoring.");
      }
      catch (Exception ex)
      {
        Log.Error(ex, "[CloseSocketSafely] Unexpected error while closing WebSocket.");
      }
    }
  }
}

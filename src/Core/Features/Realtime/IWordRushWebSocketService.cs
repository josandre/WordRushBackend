using System.Net.WebSockets;

public interface IWordRushWebSocketService
{
  GameRoom? GetRoom(string roomId);

  Task HandleConnectionAsync(WebSocket socket);
}

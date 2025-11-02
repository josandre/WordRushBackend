using System.Net.WebSockets;

/// <summary>
/// Base for a WebSocket service.
/// </summary>
public interface IWordRushWebSocketService
{
  /// <summary>
  /// Handles a new WebSocket connection and message loop.
  /// </summary>
  Task HandleConnectionAsync(WebSocket socket);
}

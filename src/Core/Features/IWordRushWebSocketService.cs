using System.Net.WebSockets;

namespace WordRush.Core.Features
{
  public interface IWordRushWebSocketService
  {
    /// <summary>
    /// Handles an incoming WebSocket connection.
    /// </summary>
    /// <param name="webSocket">The connected WebSocket.</param>
    Task HandleConnectionAsync(WebSocket webSocket);
  }
}

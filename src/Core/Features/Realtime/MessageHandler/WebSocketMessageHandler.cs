using System.Net.WebSockets;

namespace WordRush.Core.Features.Realtime.MessageHandler
{
  public abstract class WebSocketMessageHandler
  {
    /// <summary>
    /// Used for specialized logic when dealing with a specific web socket message
    /// </summary>
    /// <param name="webSocketService">Direct reference to the application web socket service</param>
    /// <param name="socket">Socket used for the message</param>
    /// <param name="userID">The user ID who sent the message</param>
    /// <param name="action">The action that will be executed by this handler</param>
    /// <param name="jsonData">Optional data received from the web socket message</param>
    public abstract Task HandleSocketMessage(WordRushWebSocketService webSocketService, WebSocket socket, string userID, string action, string jsonData);
  }
}

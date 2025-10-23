using System.Net.WebSockets;
using System.Threading.Tasks;

namespace WordRush.Core.Features
{
  public interface IWordRushWebSocketService
  {
    /// <summary>
    /// Handles a new WebSocket connection and message loop.
    /// </summary>
    Task HandleConnectionAsync(WebSocket socket);

    /// <summary>
    /// Broadcasts a text message to all connected users in a given room.
    /// </summary>
    Task BroadcastAsync(string roomId, string message);

    /// <summary>
    /// Closes a room (only by the owner) and disconnects all participants.
    /// </summary>
    Task CloseRoomAsync(string roomId);

    /// <summary>
    /// Lists all connected users in a given room.
    /// </summary>
    Task<IList<string>> GetUsersInRoomAsync(string roomId);

    /// <summary>
    /// Sends a message to a single user identified by their ID.
    /// </summary>
    Task SendToUserAsync(string userId, string message);
  }
}

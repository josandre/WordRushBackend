using System.Net.WebSockets;
using System.Collections.Concurrent;

namespace WordRush.Web.Features.WebSockets
{
  public class GameRoom
  {
    public string RoomId { get; } = Guid.NewGuid().ToString();
    public string OwnerUserId { get; set; } = string.Empty;
    public List<WebSocket> Participants { get; } = new();

    public bool IsEmpty => Participants.Count == 0;

    public void Add(WebSocket socket)
    {
      lock (Participants)
      {
        if (!Participants.Contains(socket))
        {
          Participants.Add(socket);
        }
      }
    }

    public void Remove(WebSocket socket)
    {
      lock (Participants)
      {
        Participants.Remove(socket);
      }
    }

    public List<string> GetUserIds(ConcurrentDictionary<WebSocket, string> userMap)
    {
      lock (Participants)
      {
        return Participants
            .Where(userMap.ContainsKey)
            .Select(s => userMap[s])
            .ToList();
      }
    }
  }
}

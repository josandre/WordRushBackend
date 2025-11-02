using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using WordRush.Core.Features.Realtime.Models;

namespace WordRush.Core.Features.Realtime.MessageHandler
{
  internal class GameWebSocketMessageHandler : WebSocketMessageHandler
  {
    public override async Task HandleSocketMessage(WordRushWebSocketService webSocketService, WebSocket socket, string userID, string action, string jsonData)
    {
      Console.WriteLine("SI ENTRA AL TIPO 'GAME'");

      if (action == "TEST")
      {
        Console.WriteLine(" SI RECIBE LA ACCION TEST");
      }
    }
  }
}

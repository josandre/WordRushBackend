using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using WordRush.Core.Features;

namespace WordRush.Web.Endpoints
{
  public static class WebSocketEndpoints
  {
    public static void MapWebSocketEndpoints(this WebApplication app)
    {
      app.Map("/ws", async context =>
      {
        if (context.WebSockets.IsWebSocketRequest)
        {
          var wsService = context.RequestServices.GetRequiredService<IWordRushWebSocketService>();
          using var socket = await context.WebSockets.AcceptWebSocketAsync();
          await wsService.HandleConnectionAsync(socket);
        }
        else
        {
          context.Response.StatusCode = 400;
        }
      });
    }
  }
}

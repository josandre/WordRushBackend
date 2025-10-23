using System.Net.WebSockets;
using WordRush.Core.Features;

namespace WordRush.Web.Endpoints
{
  public static class WebSocketEndpoints
  {
    public static void MapWebSocketEndpoints(this IEndpointRouteBuilder app)
    {
      app.Map("/ws", async context =>
      {
        if (context.WebSockets.IsWebSocketRequest)
        {
          IWordRushWebSocketService wsService =
              context.RequestServices.GetRequiredService<IWordRushWebSocketService>();

          WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();
          await wsService.HandleConnectionAsync(webSocket);
        }
        else
        {
          context.Response.StatusCode = StatusCodes.Status400BadRequest;
        }
      });
    }
  }
}

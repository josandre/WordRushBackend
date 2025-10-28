using Microsoft.AspNetCore.Mvc;
using WordRush.Core.Features.Realtime;

namespace WordRush.Web.Controllers;

[ApiController]
[Route("ws")]
public class WebSocketController(IWordRushWebSocketService wsService) : ApiControllerBase
{
  [HttpGet]
  public async Task HandleAsync()
  {
    var context = HttpContext;
    if (!context.WebSockets.IsWebSocketRequest)
    {
      context.Response.StatusCode = StatusCodes.Status400BadRequest;
      await context.Response.WriteAsync("Expected a WebSocket request");
      return;
    }

    using var socket = await context.WebSockets.AcceptWebSocketAsync();
    await wsService.HandleConnectionAsync(socket);
  }
}

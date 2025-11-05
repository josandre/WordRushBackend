using System.Net.WebSockets;
using System.Text.Json;
using WordRush.Core.Features.Realtime.Models.GameSession;

namespace WordRush.Core.Features.Realtime.MessageHandler
{
  internal class GameSessionWebSocketMessageHandler : WebSocketMessageHandler
  {
    public override async Task HandleSocketMessage(WordRushWebSocketService webSocketService, WebSocket socket, string userID, string action, string jsonData)
    {
      if (!Enum.TryParse(action, out WebSocketMessageTypeEnums.GameSessionClientActions result))
      {
        Console.WriteLine("ERROR: Undefined action for Game cateogory: " + action);
        return;
      }

      switch (result)
      {
        case WebSocketMessageTypeEnums.GameSessionClientActions.READY_FOR_NEXT_ROUND:
          await OnPlayerReadyForNextRound(webSocketService, userID);
          break;
        case WebSocketMessageTypeEnums.GameSessionClientActions.STOP:
          await OnPlayerStop(webSocketService, userID);
          break;
        case WebSocketMessageTypeEnums.GameSessionClientActions.SEND_ROUND_ANSWERS:
          await OnPlayerSentRoundAnswers(webSocketService, userID, jsonData);
          break;
      }
    }

    public async Task OnPlayerReadyForNextRound(WordRushWebSocketService webSocketService, string userID)
    {
      // Some States in the session should be executed only when all the players are ready
      // For example, when the players are redirected to the game room (it may take longer for one player to load, so it's better to start when everyone is ready)
      // Another example is when the players are looking for the round results, the game will continue only after everyone has reviewed their answers
      if (webSocketService.UserToRoom.TryGetValue(userID, out string roomID))
      {
        GameRoom room = webSocketService.GetRoom(roomID);
        if (room == null)
        {
          return;
        }

        // Mark the player as ready and also check if the next phase has started
        if (!room.OnPlayerReadyForNextPhase(userID))
        {
          return;
        }

        SessionState sessionState = room.GetSessionState();

        if (sessionState == SessionState.InRound)
        {
          // Send message
          string messageCategory = WebSocketMessageTypeEnums.Categories.GAME_ROOM.ToString();
          string messageAction = WebSocketMessageTypeEnums.GameSessionServerActions.ROUND_STARTED.ToString();

          // Broadcast the new phase to all players in the room
          WebSocketMessage message = new(messageCategory, messageAction, "{}");
          await webSocketService.BroadcastToRoomAsync(room.RoomId, JsonSerializer.Serialize(message));
        }
      }
    }

    public async Task OnPlayerStop(WordRushWebSocketService webSocketService, string userID)
    {
      if (webSocketService.UserToRoom.TryGetValue(userID, out string roomID))
      {
        GameRoom room = webSocketService.GetRoom(roomID);
        if (room == null)
        {
          return;
        }

        // A Stop can be registered only once per round
        SessionState sessionState = room.GetSessionState();
        if (sessionState != SessionState.InRound)
        {
          return;
        }

        // Mark the player as ready and also check if the next phase has started
        room.OnPlayerStop();

        // Send message
        string messageCategory = WebSocketMessageTypeEnums.Categories.GAME_ROOM.ToString();
        string messageAction = WebSocketMessageTypeEnums.GameSessionServerActions.ON_STOP.ToString();

        // Broadcast the stop to all the players, so they can send their answers to be processed
        WebSocketMessage message = new(messageCategory, messageAction, "{}");
        await webSocketService.BroadcastToRoomAsync(room.RoomId, JsonSerializer.Serialize(message));
      }
    }

    public async Task OnPlayerSentRoundAnswers(WordRushWebSocketService webSocketService, string userID, string jsonData)
    {
      // TODO: Deserialize the result
      // Notify the result
      // If all the players have sent the results, evaluate the answers and calculate the scores
      // After the score is calculated, send the scores to all the players
    }
  }
}

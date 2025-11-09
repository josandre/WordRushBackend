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
          RoundStartedEvent jsonData = new();
          jsonData.RoundLetter = room.Session.GetActiveRound().Letter;

          // Send message
          string messageCategory = WebSocketMessageTypeEnums.Categories.GAME_SESSION.ToString();
          string messageAction = WebSocketMessageTypeEnums.GameSessionServerActions.ROUND_STARTED.ToString();

          Console.WriteLine($"Starting round with letter: {jsonData.RoundLetter}");

          // Broadcast the new phase to all players in the room
          WebSocketMessage message = new(messageCategory, messageAction, JsonSerializer.Serialize(jsonData));
          await webSocketService.BroadcastToRoomAsync(room.RoomId, JsonSerializer.Serialize(message));
        }
        else if (sessionState == SessionState.InGameResults)
        {
          // Send message
          string messageCategory = WebSocketMessageTypeEnums.Categories.GAME_SESSION.ToString();
          string messageAction = WebSocketMessageTypeEnums.GameSessionServerActions.GAME_FINISHED.ToString();

          Console.WriteLine($"GAME FINISHED");

          // Broadcast the new phase to all players in the room
          WebSocketMessage message = new(messageCategory, messageAction, "{}");
          await webSocketService.BroadcastToRoomAsync(room.RoomId, JsonSerializer.Serialize(message));
        }
      }
    }

    public async Task OnPlayerStop(WordRushWebSocketService webSocketService, string userID)
    {
      Console.WriteLine("STOP CALLED FROM CLIENT WAHOO");
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
        string messageCategory = WebSocketMessageTypeEnums.Categories.GAME_SESSION.ToString();
        string messageAction = WebSocketMessageTypeEnums.GameSessionServerActions.ON_STOP.ToString();

        // Broadcast the stop to all the players, so they can send their answers to be processed
        WebSocketMessage message = new(messageCategory, messageAction, "{}");
        await webSocketService.BroadcastToRoomAsync(room.RoomId, JsonSerializer.Serialize(message));
      }
    }

    public async Task OnPlayerSentRoundAnswers(WordRushWebSocketService webSocketService, string userID, string jsonData)
    {
      // Deserialize the result
      // Notify the result
      // If all the players have sent the results, evaluate the answers and calculate the scores
      // After the score is calculated, send the scores to all the players
      Console.WriteLine("----USER JUST SENT ITS ANSWERS:");
      Console.WriteLine($"{userID} {jsonData}");

      if (webSocketService.UserToRoom.TryGetValue(userID, out string roomID))
      {
        GameRoom room = webSocketService.GetRoom(roomID);
        if (room == null)
        {
          return;
        }

        PlayerSentRoundAnswersEvent receivedEvent = JsonSerializer.Deserialize<PlayerSentRoundAnswersEvent>(jsonData);
        Console.WriteLine($"RECEIVED ANSWERS: {receivedEvent.Answers.Count}");
        List<GameAnswer> answers = new();
        for (int i = 0; i < receivedEvent.Answers.Count; i++)
        {
          answers.Add(new(room.Settings.CategoriesArray[i], receivedEvent.Answers[i]));
        }

        GameRoundResult result = new();
        result.User = room.Players[userID];
        result.Answers = answers;

        // Check if all the players have registered their answers
        if (!room.RegisterPlayerRoundResult(userID, result))
        {
          return;
        }

        Console.WriteLine("All players have sent their answers, starting evaluation");

        // The data is ready to start the evaluation
        // TODO: FRANCISCO Wait here until the system finishes with the evaluation, for now simply notify that the round has finished

        Console.WriteLine("Evaluation finished, returning results");

        // Send round results message
        string messageCategory = WebSocketMessageTypeEnums.Categories.GAME_SESSION.ToString();
        string messageAction = WebSocketMessageTypeEnums.GameSessionServerActions.ROUND_RESULTS_SENT.ToString();

        // Broadcast the new phase to all players in the room
        WebSocketMessage message = new(messageCategory, messageAction, JsonSerializer.Serialize(room.Session.GetActiveRound()));
        await webSocketService.BroadcastToRoomAsync(room.RoomId, JsonSerializer.Serialize(message));
      }
    }
  }
}

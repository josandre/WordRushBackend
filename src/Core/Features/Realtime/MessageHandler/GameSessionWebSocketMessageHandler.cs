using System.Net.WebSockets;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using WordRush.Core.Features.Realtime.Models.GameSession;
using WordRush.Core.Features.Scoring;
using WordRush.Core.Features.Realtime.Models;
using WordRush.Core.Features.Hints;

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
        case WebSocketMessageTypeEnums.GameSessionClientActions.REQUEST_HINT:
          await OnPlayerRequestedHint(webSocketService, socket, userID, jsonData);
          break;
        default: break;
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
          string messageCategory = WebSocketMessageTypeEnums.Categories.GAME_SESSION.ToString();
          string messageAction = WebSocketMessageTypeEnums.GameSessionServerActions.GAME_FINISHED.ToString();

          Console.WriteLine($"GAME FINISHED");

          // 🔹 Build final leaderboard
          List<GamePlayerScore> finalScores = room.Session.GetAllScores();

          // Update game statistics for all players
          await UpdateGameStatisticsAsync(webSocketService, room, finalScores);

          var finalResponse = new
          {
            Players = finalScores
          };

          WebSocketMessage message = new(messageCategory, messageAction,
              JsonSerializer.Serialize(finalResponse));

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
        using (var scope = webSocketService.ServiceScopeFactory.CreateScope())
        {
          var scoringService = scope.ServiceProvider.GetRequiredService<IScoringService>();

          try
          {
            // Build scoring request
            GameRound currentRound = room.Session.GetActiveRound();
            var request = new WordRush.Core.Features.Scoring.Models.StopGameRequest
            {
              Letter = currentRound.Letter,
              Categories = room.Settings.CategoriesArray.ToList(),
              Players = currentRound.Results.Select(r => new WordRush.Core.Features.Scoring.Models.PlayerEntry
              {
                Name = r.User.Nickname,
                UserId = r.User.UserId,
                Answers = r.Answers.ToDictionary(a => a.Category, a => a.Answer)
              }).ToList()
            };

            Log.Information("[SCORING] Starting AI evaluation for round {Letter} in room {RoomId}", currentRound.Letter, room.RoomId);

            // Evaluate via Ollama AI model
            WordRush.Core.Features.Scoring.Models.StopGameResponse? scoredResponse = await scoringService.ScoreGameAsync(request);

            if (scoredResponse != null)
            {
              Log.Information("[SCORING] Round {Letter} evaluated successfully for room {RoomId}", currentRound.Letter, room.RoomId);

              // 🔹 Update cumulative totals for each player in the session
              foreach (var scoredPlayer in scoredResponse.Players)
              {
                room.Session.AddOrUpdatePlayerScore(scoredPlayer.Name, scoredPlayer.Total);
              }

              // ✅ Optional: log current standings
              foreach (var p in room.Session.Players.Values)
              {
                Log.Information("[STANDINGS] {Name}: {Score}", p.Nickname, p.TotalScore);
              }

              // Then broadcast round results...
              string scoreMessageCategory = WebSocketMessageTypeEnums.Categories.GAME_SESSION.ToString();
              string scoreMessageAction = WebSocketMessageTypeEnums.GameSessionServerActions.ROUND_RESULTS_SENT.ToString();

              WebSocketMessage scoredMessage = new(
                  scoreMessageCategory,
                  scoreMessageAction,
                  JsonSerializer.Serialize(scoredResponse, webSocketService.JsonOptions));

              await webSocketService.BroadcastToRoomAsync(
                  room.RoomId,
                  JsonSerializer.Serialize(scoredMessage, webSocketService.JsonOptions));
            }
            else
            {
              Log.Warning("[SCORING] Null result returned for round {Letter} in room {RoomId}", currentRound.Letter, room.RoomId);
            }
          }
          catch (Exception ex)
          {
            Log.Error(ex, "[SCORING] Error while evaluating round {Letter} in room {RoomId}", room.Session.GetActiveRound().Letter, room.RoomId);

            string scoreMessageCategory = WebSocketMessageTypeEnums.Categories.GAME_SESSION.ToString();
            string scoreMessageAction = WebSocketMessageTypeEnums.GameSessionServerActions.ROUND_RESULTS_SENT.ToString();

            WebSocketMessage errorMessage = new(
                scoreMessageCategory,
                scoreMessageAction,
                JsonSerializer.Serialize(new { error = "Scoring service failed to evaluate round." }));

            await webSocketService.BroadcastToRoomAsync(
                room.RoomId,
                JsonSerializer.Serialize(errorMessage, webSocketService.JsonOptions));
          }
        }
      }
    }

    private async Task UpdateGameStatisticsAsync(WordRushWebSocketService webSocketService, GameRoom room, List<GamePlayerScore> finalScores)
    {
      if (finalScores == null || finalScores.Count == 0)
      {
        return;
      }

      // Find the winner (player with highest TotalScore)
      GamePlayerScore? winner = finalScores.OrderByDescending(p => p.TotalScore).FirstOrDefault();
      if (winner == null)
      {
        return;
      }

      using (var scope = webSocketService.ServiceScopeFactory.CreateScope())
      {
        var gameStatisticsService = scope.ServiceProvider.GetRequiredService<IGameStatisticsService>();

        // Update statistics for all players
        foreach (var playerScore in finalScores)
        {
          // Find the UserId by matching nickname in room.Players
          int? userId = null;
          foreach (var kvp in room.Players)
          {
            if (string.Equals(kvp.Value.Nickname, playerScore.Nickname, StringComparison.OrdinalIgnoreCase))
            {
              userId = kvp.Value.UserId;
              break;
            }
          }

          if (userId.HasValue)
          {
            try
            {
              bool won = string.Equals(playerScore.Nickname, winner.Nickname, StringComparison.OrdinalIgnoreCase);
              await gameStatisticsService.UpdateGameStatisticsAsync(userId.Value, won, playerScore.TotalScore);
              
              Log.Information(
                "[STATISTICS] Updated stats for {Nickname} (UserId: {UserId}): Won={Won}, Score={Score}",
                playerScore.Nickname, userId.Value, won, playerScore.TotalScore);
            }
            catch (Exception ex)
            {
              Log.Error(ex, "[STATISTICS] Failed to update statistics for {Nickname} (UserId: {UserId})",
                playerScore.Nickname, userId);
            }
          }
          else
          {
            Log.Warning("[STATISTICS] Could not find UserId for player {Nickname}", playerScore.Nickname);
          }
        }
      }
    }
    /// <summary>
    /// Handles a hint request from a client.  
    /// Validates the player's remaining hint tokens, invokes the AI hint service
    /// and sends the generated hint back only to the requesting socket.  
    /// If no tokens remain, a message indicating that no hints are left is returned.
    /// </summary>
    public async Task OnPlayerRequestedHint(
    WordRushWebSocketService webSocketService,
    WebSocket socket,
    string userID,
    string jsonData)
    {
      // Determine which room the user belongs to
      if (!webSocketService.UserToRoom.TryGetValue(userID, out string roomID))
      {
        Log.Warning("[HINT] User {UserID} is not assigned to any room.", userID);
        return;
      }

      GameRoom? room = webSocketService.GetRoom(roomID);
      if (room == null)
      {
        Log.Warning("[HINT] Room {RoomID} not found for user {UserID}.", roomID, userID);
        return;
      }

      // Deserialize the incoming hint request (category + letter)
      HintRequest? request = null;
      try
      {
        request = JsonSerializer.Deserialize<HintRequest>(jsonData, webSocketService.JsonOptions);
      }
      catch (Exception ex)
      {
        Log.Warning(ex, "[HINT] Failed to deserialize hint request for user {UserID}. Raw: {JsonData}", userID, jsonData);
      }

      string category = request?.Category?.Trim() ?? string.Empty;
      string letter = request?.Letter?.Trim() ?? string.Empty;

      // If letter not provided, use the active round's letter
      if (string.IsNullOrWhiteSpace(letter))
      {
        try
        {
          var activeRound = room.Session.GetActiveRound();
          letter = activeRound?.Letter ?? letter;
        }
        catch (Exception ex)
        {
          Log.Warning(ex, "[HINT] Could not fetch active round letter for user {UserID}.", userID);
        }
      }

      Log.Information("[HINT] Player {UserID} requested hint. Category='{Category}', Letter='{Letter}'", userID, category, letter);

      // Check if the player has tokens remaining
      if (!room.UseHint(userID))
      {
        Log.Information("[HINT] Player {UserID} has no tokens left.", userID);
        var noTokensResponse = new
        {
          hint = "",
          tokensLeft = room.GetRemainingHints(userID)
        };
        WebSocketMessage noTokensWs = new(
            WebSocketMessageTypeEnums.Categories.GAME_SESSION.ToString(),
            WebSocketMessageTypeEnums.GameSessionServerActions.HINT_RESPONSE.ToString(),
            JsonSerializer.Serialize(noTokensResponse, webSocketService.JsonOptions)
        );
        await webSocketService.SendAsync(socket, JsonSerializer.Serialize(noTokensWs, webSocketService.JsonOptions));
        return;
      }

      // Acquire a hint via the hint service
      using var scope = webSocketService.ServiceScopeFactory.CreateScope();
      var hintService = scope.ServiceProvider.GetRequiredService<WordRush.Core.Features.Hints.IHintService>();
      string? hint = null;

      try
      {
        hint = await hintService.GetHintAsync(letter, category);
        Log.Information("[HINT] Model returned hint for {UserID}: {Hint}", userID, hint);
      }
      catch (Exception ex)
      {
        Log.Warning(ex, "[HINT] HintService exception for {UserID}. Category='{Category}', Letter='{Letter}'", userID, category, letter);
      }

      // Fallback text if model failed
      if (string.IsNullOrWhiteSpace(hint))
      {
        hint = "No hint available.";
        Log.Warning("[HINT] Empty hint result for {UserID}. Category='{Category}', Letter='{Letter}'", userID, category, letter);
      }

      var response = new
      {
        hint,
        tokensLeft = room.GetRemainingHints(userID)
      };

      WebSocketMessage ws = new(
          WebSocketMessageTypeEnums.Categories.GAME_SESSION.ToString(),
          WebSocketMessageTypeEnums.GameSessionServerActions.HINT_RESPONSE.ToString(),
          JsonSerializer.Serialize(response, webSocketService.JsonOptions)
      );

      await webSocketService.SendAsync(socket, JsonSerializer.Serialize(ws, webSocketService.JsonOptions));
    }

  }
}

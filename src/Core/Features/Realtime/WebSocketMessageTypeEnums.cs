namespace WordRush.Core.Features.Realtime
{
  public class WebSocketMessageTypeEnums
  {
    /// <summary>
    /// First part of the type, it determines which message handler will take care of the received event.
    /// </summary>
    public enum Categories
    {
      GAME_ROOM,
      GAME_SESSION
    }

    /// <summary>
    /// Actions that can be handled by the GameRoom message handler.
    /// </summary>
    public enum GameRoomClientActions
    {
      CREATE,
      JOIN,
      LEAVE,
      TOGGLE_READY,
      REQUEST_DATA,
      START_GAME
    }

    public enum GameRoomServerActions
    {
      CREATED,
      JOINED,
      JOINED_NON_EXISTING_ROOM,
      DATA_UPDATED,
      CLOSED,
      GAME_STARTED
    }

    /// <summary>
    /// Actions that can be handled by the Game message handler.
    /// </summary>
    public enum GameSessionClientActions
    {
      READY_FOR_NEXT_ROUND,
      STOP,
      SEND_ROUND_ANSWERS
    }

    public enum GameSessionServerActions
    {
      ROUND_STARTED,
      ON_STOP,
      ROUND_FINISHED,
      ROUND_RESULTS_SENT,
      GAME_FINISHED
    }
  }
}

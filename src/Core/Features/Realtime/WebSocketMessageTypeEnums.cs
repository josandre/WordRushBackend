namespace WordRush.Core.Features.Realtime
{
  public class WebSocketMessageTypeEnums
  {
    /// <summary>
    /// First part of the type, it determines which message handler will take care of the event.
    /// </summary>
    public enum Categories
    {
      GAME_ROOM,
      GAME
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
      REQUEST_DATA
    }

    public enum GameRoomServerActions
    {
      CREATED,
      JOINED,
      JOINED_NON_EXISTING_ROOM,
      DATA_UPDATED,
      CLOSED
    }

    /// <summary>
    /// Actions that can be handled by the Game message handler.
    /// </summary>
    public enum GameClientActions
    {
      STOP,
    }

    public enum GameServerActions
    {
      ON_ROUND_START,
      ON_STOP
    }
  }
}

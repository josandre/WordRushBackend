namespace WordRush.Core.Features.Realtime
{
  /// <summary>
  /// Used to exchange information through WebSocket, this is the class that will be sent to a client.
  /// </summary>
  [Serializable]
  public class WebSocketMessage
  {
    public string Type { get; set; } // The action or unique identifier for the message

    public string JsonData { get; set; } // If required, the additional data in the message

    public WebSocketMessage()
    {
      Type = string.Empty;
      JsonData = string.Empty;
    }

    public WebSocketMessage(string category, string action, string jsonData)
    {
      Type = category + "|" + action;
      JsonData = jsonData;
    }
  }
}

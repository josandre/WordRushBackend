using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WordRush.Core.Features.Realtime.Models.JoinRoom
{
  [Serializable]
  public class GameRoomJoinedEvent
  {
    public string GameRoomID { get; set; }
  }
}

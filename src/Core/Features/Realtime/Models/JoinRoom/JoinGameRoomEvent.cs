using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WordRush.Core.Features.Realtime.Models.JoinRoom
{
  [Serializable]
  public class JoinGameRoomEvent
  {
    public UserProfile PlayerProfile { get; set; } // The user that is requesting the room creation
    public string RoomID { get; set; } // The id of the room to check if the player can join
  }
}

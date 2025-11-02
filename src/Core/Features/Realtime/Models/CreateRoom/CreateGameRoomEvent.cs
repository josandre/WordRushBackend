using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WordRush.Core.Features.Realtime.Models.CreateRoom
{
  [Serializable]
  public class CreateGameRoomEvent
  {
    public UserProfile PlayerProfile { get; set; }
  }
}

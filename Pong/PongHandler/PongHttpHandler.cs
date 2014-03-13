using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Pong.PongHandler
{
    /// <summary>
    /// Http handler accepting web socket requests
    /// </summary>
    public class PongHttpHandler : IHttpHandler
    {
        public bool IsReusable
        {
            get { return false; }
        }

        public void ProcessRequest(HttpContext context)
        {
            if (context.IsWebSocketRequest)
            {
                // create a player
                var player = new PongPlayer();
                PongApp.JoinPlayer(player);

                // start receiving from socket
                context.AcceptWebSocketRequest(player.Receiver);
            }
        }
    }
}
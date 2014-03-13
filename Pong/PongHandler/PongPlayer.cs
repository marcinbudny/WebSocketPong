using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.WebSockets;
using Newtonsoft.Json;

namespace Pong.PongHandler
{
    /// <summary>
    /// Represents a player and wraps his websocket operations
    /// </summary>
    public class PongPlayer
    {
        private PongGame _game;
        private object _syncRoot = new object();
        private AspNetWebSocketContext _context;

        public event Action<PongPlayer, PlayerPositionMessage> PlayerMoved;
        public event Action<PongPlayer> PlayerDisconnected;

        // player vertical positon
        public int YPos { get; private set; }

        public void SetGame(PongGame game)
        {
            if (_game != null)
                throw new InvalidOperationException();
            _game = game;
        }

        /// <summary>
        /// This method is used as delegate to accept WebSocket connection
        /// </summary>
        public async Task Receiver(AspNetWebSocketContext context)
        {
            _context = context;
            var socket = _context.WebSocket as AspNetWebSocket;
            // prepare buffer for reading messages
            var inputBuffer = new ArraySegment<byte>(new byte[1024]);

            // send player number to player
            SendMessage(new PlayerNumberMessage { PlayerNumber = _game.GetPlayerIndex(this) });

            try
            {
                while (true)
                {
                    // read from socket
                    var result = await socket.ReceiveAsync(inputBuffer, CancellationToken.None);
                    if (socket.State != WebSocketState.Open)
                    {
                        if (PlayerDisconnected != null)
                            PlayerDisconnected(this);
                        break;
                    }

                    // convert bytes to text
                    var messageString = Encoding.UTF8.GetString(inputBuffer.Array, 0, result.Count);
                    // only PlayerPositionMessage is expected, deserialize
                    var positionMessage = JsonConvert.DeserializeObject<PlayerPositionMessage>(messageString);

                    // save new position and notify game
                    YPos = positionMessage.YPos;
                    if (PlayerMoved != null)
                        PlayerMoved(this, positionMessage);

                }
            }
            catch (Exception ex)
            {
                if (PlayerDisconnected != null)
                    PlayerDisconnected(this);
            }
        }

        /// <summary>
        /// Sends message through web socket to player's rowser
        /// </summary>
        public async Task SendMessage(object message)
        {
            // serialize and send
            var messageString = JsonConvert.SerializeObject(message);
            if (_context != null && _context.WebSocket.State == WebSocketState.Open)
            {
                var outputBuffer = new ArraySegment<byte>(Encoding.UTF8.GetBytes(messageString));
                await _context.WebSocket.SendAsync(outputBuffer, WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }

        /// <summary>
        /// Closes player's web socket
        /// </summary>
        public void Close()
        {
            if (_context != null && _context.WebSocket.State == WebSocketState.Open)
            {
                _context.WebSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Closing...", CancellationToken.None).Wait();
            }
        }
    }
}
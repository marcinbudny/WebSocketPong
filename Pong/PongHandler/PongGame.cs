using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace Pong.PongHandler
{
    public enum GameState
    {
        NotInitiated,
        WaitingForPlayer,
        InProgress
    }
    
    /// <summary>
    /// Represents pong game between two players
    /// </summary>
    public class PongGame
    {
        public const int LeftPlayer = 0;
        public const int RightPlayer = 1;
        
        public const int FieldWidth = 400;
        public const int FieldHeight = 300;
        public const int PlayerToEdgeDistance = 5;
        public const int PlayerWidth = 6;
        public const int PlayerHeight = 50;
        public const int PlayerReach = PlayerToEdgeDistance + PlayerWidth;
        public const int BallRadius = 3;
        public const double BallStartingSpeedPixPerSecond = 40;
        public const double BallSpeedIncrease = 5;
        
        private object _syncRoot = new object();
        private PongPlayer[] _players = new PongPlayer[2];
        private Vector _ballPosition = new Vector(FieldWidth / 2, FieldHeight / 2);
        private Vector _ballDirection = Vector.SW;
        private double _ballSpeed = BallStartingSpeedPixPerSecond;
        private int[] _score = new int[2];

        /// <summary>
        /// Token used to cances ball moving task
        /// </summary>
        private CancellationTokenSource _ballCancellationTokenSource;
        
        public GameState State { get; private set; }

        /// <summary>
        /// Called when game is over
        /// </summary>
        public event Action<PongGame> GameOver;


        public PongGame()
        {
            State = GameState.NotInitiated;
        }

        public int GetPlayerIndex(PongPlayer player)
        {
            return Array.IndexOf(_players, player);
        }

        public PongPlayer OtherPlayer(PongPlayer thisPlayer)
        {
            var index = GetPlayerIndex(thisPlayer);
            return _players[index == LeftPlayer ? RightPlayer : LeftPlayer];
        }

        /// <summary>
        /// Joins new player to this game
        /// </summary>
        /// <param name="player"></param>
        public void JoinPlayer(PongPlayer player)
        {
            lock (_syncRoot)
            {
                if (_players[LeftPlayer] != null && _players[RightPlayer] != null)
                    throw new InvalidOperationException();

                player.SetGame(this);
                player.PlayerDisconnected += OnPlayerDisconnected;
                player.PlayerMoved += OnPlayerMoved;

                if (_players[LeftPlayer] == null)
                {
                    _players[LeftPlayer] = player;
                    State = GameState.WaitingForPlayer;
                }
                else
                {
                    _players[RightPlayer] = player;
                    // we have two players, so start the game
                    State = GameState.InProgress; 
                    StartBall();
                }
            }
        }

        private void StartBall()
        {
            // this delegete is responsible for ball moving, bouncing and counting score
            Action<CancellationToken> ballMover = (cancellationToken) =>
                {
                    var lastTime = DateTime.Now;

                    while(true)
                    {
                        var thisTime = DateTime.Now;
                        // how many seconds elapsed since last pass
                        var secondsElapsed = (thisTime - lastTime).TotalMilliseconds / 1000.0; 

                        MoveBall(secondsElapsed);
                        lastTime = thisTime;

                        Thread.Sleep(50);

                        // finish task if cancel requested
                        if (cancellationToken.IsCancellationRequested)
                            break;
                    }
                };

            // prepare cancellation token and run the task
            _ballCancellationTokenSource = new CancellationTokenSource();
            Task.Factory.StartNew(() => ballMover(_ballCancellationTokenSource.Token), _ballCancellationTokenSource.Token, 
                TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        private void MoveBall(double secondsElapsed)
        {
            lock (_syncRoot)
            {
                // calculate new position
                _ballPosition += _ballDirection * (_ballSpeed * secondsElapsed);

                // check for collisions
                // up
                if(_ballPosition.Y < BallRadius)
                {
                    _ballPosition += new Vector(0, -(_ballPosition.Y - BallRadius));
                    _ballDirection = _ballDirection.MirrorY();
                }
                // down
                if (_ballPosition.Y > FieldHeight - BallRadius) 
                {
                    _ballPosition += new Vector(0, -(_ballPosition.Y - (FieldHeight - BallRadius)));
                    _ballDirection = _ballDirection.MirrorY();
                }
                // left player
                if (_ballPosition.X < PlayerReach + BallRadius &&
                    _ballPosition.Y <= _players[LeftPlayer].YPos + (PlayerHeight / 2) && _ballPosition.Y >= _players[LeftPlayer].YPos - (PlayerHeight / 2))
                {
                    _ballPosition += new Vector(-(_ballPosition.X - (BallRadius + PlayerReach)), 0);
                    _ballDirection = _ballDirection.MirrorX();
                    // speed things up to make them more interesing
                    _ballSpeed += BallSpeedIncrease;
                }
                // right player
                if (_ballPosition.X > FieldWidth - (BallRadius + PlayerReach) &&
                    _ballPosition.Y <= _players[RightPlayer].YPos + (PlayerHeight / 2) && _ballPosition.Y >= _players[RightPlayer].YPos - (PlayerHeight / 2))
                {
                    _ballPosition += new Vector(-(_ballPosition.X - (FieldWidth - (BallRadius + PlayerReach))), 0);
                    _ballDirection = _ballDirection.MirrorX();
                    // speed things up to make them more interesing
                    _ballSpeed += BallSpeedIncrease;
                }

                // check for scores
                if (_ballPosition.X < 0 || _ballPosition.X > FieldWidth)
                {
                    _score[_ballPosition.X < 0 ? RightPlayer : LeftPlayer]++;
                    // broadcast score message
                    BroadcastMessage(new ScoreMessage { Score = _score });

                    //reset ball
                    var random = new Random();
                    _ballPosition = new Vector(FieldWidth / 2, BallRadius + random.Next(FieldHeight - 2 * BallRadius));
                    _ballDirection = Vector.Directions[random.Next(Vector.Directions.Length - 1)];
                    _ballSpeed = BallStartingSpeedPixPerSecond;
                }

                // broadcast ball position message
                BroadcastMessage(new BallPositionMessage { XPos = (int)_ballPosition.X, YPos = (int)_ballPosition.Y });
            }
        }

        /// <summary>
        /// Sends a message to both players
        /// </summary>
        /// <param name="message"></param>
        private void BroadcastMessage(object message)
        {
            foreach (var player in _players)
            {
                player.SendMessage(message);
            }
        }


        private void OnPlayerMoved(PongPlayer player, PlayerPositionMessage position)
        {
            var otherPlayer = OtherPlayer(player);

            if (otherPlayer != null)
            {
                // send new player position to the other player
                otherPlayer.SendMessage(new PlayerPositionMessage { YPos = player.YPos });
            }
        }

        private void OnPlayerDisconnected(PongPlayer player)
        {
            lock (_syncRoot)
            {
                // stop the ball moving task
                _ballCancellationTokenSource.Cancel();
                var otherPlayer = OtherPlayer(player);

                if (otherPlayer != null)
                {
                    // close connection to other player, which means game is over
                    otherPlayer.Close();
                }
                if (GameOver != null)
                    GameOver(this);
            }
        }
    }
}
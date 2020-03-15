/// <summary>
/// xboxSnake namespace.
/// </summary>
namespace xboxSnake
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Threading;
    using Windows.UI;
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Media;
    using Windows.UI.Xaml.Shapes;
    using UWP_Messager;
    using Windows.UI.Core;
    using Windows.System;
    using Windows.Gaming.Input;

    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        #region Variables

        /// <summary>
        /// The size of one pixle;
        /// </summary>
        private const int gamePixle = 10;

        /// <summary>
        /// The size of the grid squared.
        /// </summary>
        private const int gameSquare = 450;

        /// <summary>
        /// The min grid size.
        /// </summary>
        private const int minGrid = 0;

        /// <summary>
        /// The max grid size.
        /// </summary>
        private const int maxGrid = 44;

        /// <summary>
        /// The Score of the game.
        /// </summary>
        private int gameScore;

        /// <summary>
        /// Are we using a Xbox One?
        /// </summary>
        private bool isXbox = Windows.System.Profile.AnalyticsInfo.VersionInfo.DeviceFamily == "Windows.Xbox";

        /// <summary>
        /// The current game state.
        /// </summary>
        private GameState currentGameState = GameState.InGame;

        /// <summary>
        /// The Apple.
        /// </summary>
        private gameApple Apple;

        /// <summary>
        /// The snake.
        /// </summary>
        private List<snakeSegment> Snake;

        /// <summary>
        /// Used to draw and calculate frames.
        /// </summary>
        private BackgroundWorker gameTick;

        /// <summary>
        /// Used to create a message box.
        /// </summary>
        private UWP_MessageBox messageBox = new UWP_MessageBox();

        /// <summary>
        /// Used for reading gampad input every 10th of a second.
        /// </summary>
        private DispatcherTimer gamePad = new DispatcherTimer();

        /// <summary>
        /// The controller we are using.
        /// </summary>
        private Gamepad mainGamepad;

        /// <summary>
        /// Used for reading gamepad inputs.
        /// </summary>
        private GamepadReading reading;

        /// <summary>
        /// Is there a Xbox One Controller plugged in?
        /// </summary>
        private bool xboxController = false;

        /// <summary>
        /// Used to lock a thread to proccess gamepads.
        /// </summary>
        private readonly object myLock = new object();

        #endregion

        #region Objects

        /// <summary>
        /// Posible game states.
        /// </summary>
        private enum GameState
        {
            InGame,
            GameOver,
            Paused
        }

        /// <summary>
        /// The Movement direction of the snakes body.
        /// </summary>
        private enum MovementDirection
        {
            Up,
            Down,
            Left,
            Right
        }

        /// <summary>
        /// A peice of the snake.
        /// This needs to be a class to because the Propertys need to be of Reference types, not Static.
        /// </summary>
        private class snakeSegment
        {
            public int X;
            public int Y;
            public MovementDirection Direction;

            public snakeSegment(int conX, int conY, MovementDirection conMoveDir)
            {
                this.X = conX;
                this.Y = conY;
                this.Direction = conMoveDir;
            }

            /// <summary>
            /// Return a pixle based on the x & y.
            /// </summary>
            public Rectangle Pixle
            {
                get
                {
                    Rectangle pixle = new Rectangle();
                    pixle.Margin = new Thickness(X * gamePixle, Y * gamePixle, (gameSquare - (X * gamePixle)) - gamePixle, (gameSquare - (Y * gamePixle)) - gamePixle);
                    pixle.Fill = new SolidColorBrush(Color.FromArgb(255, 127, 255, 0));
                    return pixle;
                }
            }
        }

        /// <summary>
        /// The apple for the game.
        /// </summary>
        private struct gameApple
        {
            public int X;
            public int Y;

            public gameApple(List<snakeSegment> Snake)
            {
                this.X = 0;
                this.Y = 0;

                Random r = new Random();
                bool flag = true;
                while (flag)
                {
                    flag = false;
                    this.X = r.Next(0, 45);
                    this.Y = r.Next(0, 45);

                    foreach (snakeSegment seg in Snake)
                    {
                        if (seg.X == this.X && seg.Y == this.Y)
                        {
                            flag = true;
                            break;
                        }
                    }
                }
            }

            /// <summary>
            /// Return a pixle based on the x & y.
            /// </summary>
            public Rectangle Pixle
            {
                get
                {
                    Rectangle pixle = new Rectangle();
                    pixle.Margin = new Thickness(X * gamePixle, Y * gamePixle, (gameSquare - (X * gamePixle)) - gamePixle, (gameSquare - (Y * gamePixle)) - gamePixle);
                    pixle.Fill = new SolidColorBrush(Color.FromArgb(255, 255, 0, 0));
                    return pixle;
                }
            }
        }

        #endregion

        /// <summary>
        /// MainPage Constructor.
        /// </summary>
        public MainPage()
        {
            Windows.UI.ViewManagement.ApplicationView.GetForCurrentView().SetDesiredBoundsMode(Windows.UI.ViewManagement.ApplicationViewBoundsMode.UseCoreWindow);
            this.InitializeComponent();

            // Handles key input.
            Window.Current.CoreWindow.KeyDown += CoreWindow_KeyDown;

            // Instanciate the BackgroundWorker.
            gameTick = new BackgroundWorker()
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = true
            };

            // Define game tick logic.
            gameTick.DoWork += GameTick_DoTick;
            gameTick.ProgressChanged += GameTick_MoveSnake;
            gameTick.RunWorkerCompleted += GameTick_EndGame;

            InitiateGamepad();
            gamePad.Tick += GamePad_Tick;
            gamePad.Interval = new System.TimeSpan(TimeSpan.TicksPerSecond / 10);
            gamePad.Start();
        }

        /// <summary>
        /// Start the game.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void StartGame_Click(object sender, RoutedEventArgs e)
        {
            StartGame();
        }


        /// <summary>
        /// Pause the game.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PauseGame_Click(object sender, RoutedEventArgs e)
        {
            // I love the tenary opperator.
            currentGameState = (currentGameState == GameState.Paused) ? GameState.InGame : GameState.Paused;
        }

        /// <summary>
        /// Call this to start the game.
        /// </summary>
        private void StartGame ()
        {
            // Start a new game.
            Snake = new List<snakeSegment>();
            gameMatrix.Children.Clear();
            currentGameState = GameState.InGame;
            ScoreTxT.Text = "Score: 0";
            gameScore = 0;

            // Create the snakes body.
            Random r = new Random();
            for (/*Declaration*/int startX = r.Next(10, 35), startY = r.Next(10, 35), startDir = r.Next(0, 4), y = startY, x = startX, i = 0;/*Query*/ i < 8;/*Increment*/ y = (startDir == 0) ? y + 1 : (startDir == 1) ? y - 1 : startY, x = (startDir == 2) ? x + 1 : (startDir == 3) ? x - 1 : startX, i++)
            {
                Snake.Add(new snakeSegment(x, y, (MovementDirection)startDir));
            }

            // Draw snake.
            foreach (snakeSegment seg in Snake)
            {
                gameMatrix.Children.Add(seg.Pixle);
            }

            // Create the apple.
            Apple = new gameApple(Snake);

            // Draw apple.
            gameMatrix.Children.Add(Apple.Pixle);

            // Start game.
            if (!gameTick.IsBusy)
            {
                gameTick.RunWorkerAsync();
            }
        }

        /// <summary>
        /// This will update the snake position on every tick, or calculate loss.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void GameTick_DoTick(object sender, DoWorkEventArgs e)
        {
            // Wait one second before the snake starts to move.
            Thread.Sleep(1000);
            
            while (currentGameState == GameState.InGame || currentGameState == GameState.Paused)
            {
                if(currentGameState == GameState.InGame)
                {
                    gameTick.ReportProgress(0);
                }

                Thread.Sleep(100);
            }
        }

        /// <summary>
        /// This will draw the new snake position.
        /// </summary>
        private void GameTick_MoveSnake(object sender, ProgressChangedEventArgs e)
        {
            // Note: X & Y Go on a grid from 0 to 44
            // Move snake forward one place in the correct Direction. If that direction is a wall; Die.
            if (Snake[0].Direction == MovementDirection.Up && Snake[0].Y > minGrid)
            {
                Snake.RemoveAt(Snake.Count - 1);
                Snake.Insert(0, new snakeSegment(Snake[0].X, Snake[0].Y - 1, Snake[0].Direction));
            }

            else if (Snake[0].Direction == MovementDirection.Up && Snake[0].Y == minGrid)
            {
                currentGameState = GameState.GameOver;
            }

            else if (Snake[0].Direction == MovementDirection.Down && Snake[0].Y < maxGrid)
            {
                Snake.RemoveAt(Snake.Count - 1);
                Snake.Insert(0, new snakeSegment(Snake[0].X, Snake[0].Y + 1, Snake[0].Direction));
            }

            else if (Snake[0].Direction == MovementDirection.Down && Snake[0].Y == maxGrid)
            {
                currentGameState = GameState.GameOver;
            }

            else if (Snake[0].Direction == MovementDirection.Left && Snake[0].X > minGrid)
            {
                Snake.RemoveAt(Snake.Count - 1);
                Snake.Insert(0, new snakeSegment(Snake[0].X - 1, Snake[0].Y, Snake[0].Direction));
            }

            else if (Snake[0].Direction == MovementDirection.Left && Snake[0].X == minGrid)
            {
                currentGameState = GameState.GameOver;
            }

            else if (Snake[0].Direction == MovementDirection.Right && Snake[0].X < maxGrid)
            {
                Snake.RemoveAt(Snake.Count - 1);
                Snake.Insert(0, new snakeSegment(Snake[0].X + 1, Snake[0].Y, Snake[0].Direction));
            }

            else if (Snake[0].Direction == MovementDirection.Right && Snake[0].X == maxGrid)
            {
                currentGameState = GameState.GameOver;
            }

            //  For every segement except for the head.
            for(int i = 1; i < Snake.Count; i++)
            {
                // Handles the snake body intersecting its self.
                if (Snake[i].X == Snake[0].X && Snake[i].Y == Snake[0].Y)
                {
                    currentGameState = GameState.GameOver;
                    break;
                }

                // Make sure the snake keeps its direction.
                else
                {
                    Snake[i].Direction = Snake[i - 1].Direction;
                }
            }

            // Eat the apple.
            if(Snake[0].X == Apple.X && Snake[0].Y == Apple.Y)
            {
                if (Snake[Snake.Count - 1].Direction == MovementDirection.Up)
                {
                    Snake.Add(new snakeSegment(Snake[Snake.Count - 1].X, Snake[Snake.Count - 1].Y + 1, Snake[Snake.Count - 1].Direction));
                }

                else if (Snake[Snake.Count - 1].Direction == MovementDirection.Down)
                {
                    Snake.Add(new snakeSegment(Snake[Snake.Count - 1].X, Snake[Snake.Count - 1].Y - 1, Snake[Snake.Count - 1].Direction));
                }

                else if (Snake[Snake.Count - 1].Direction == MovementDirection.Left)
                {
                    Snake.Add(new snakeSegment(Snake[Snake.Count - 1].X + 1, Snake[Snake.Count - 1].Y, Snake[Snake.Count - 1].Direction));
                }

                else if (Snake[Snake.Count - 1].Direction == MovementDirection.Right)
                {
                    Snake.Add(new snakeSegment(Snake[Snake.Count - 1].X - 1, Snake[Snake.Count - 1].Y, Snake[Snake.Count - 1].Direction));
                }

                Apple = new gameApple(Snake);

                gameScore++;
                ScoreTxT.Text = "Score: " + gameScore;
            }

            if(currentGameState == GameState.InGame)
            {
                // Clear the matrix.
                gameMatrix.Children.Clear();

                // Draw the snake.
                foreach (snakeSegment seg in Snake)
                {
                    gameMatrix.Children.Add(seg.Pixle);
                }

                // Draw the apple.
                gameMatrix.Children.Add(Apple.Pixle);
            }
        }

        /// <summary>
        /// This will end the game.
        /// </summary>
        private void GameTick_EndGame(object sender, RunWorkerCompletedEventArgs e)
        {
            messageBox.popupMessage("You lost!\nYour score was: " + gameScore + "\nWould you like to play again?", "Snake",
            // Yes.
            () =>
            {
                StartGame();
            },

            // No.
            () =>
            {

            });
        }

        /// <summary>
        /// Handles Key inputs.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        public void CoreWindow_KeyDown(CoreWindow sender, KeyEventArgs args)
        {
            if(currentGameState == GameState.InGame)
            {
                if (args.VirtualKey == VirtualKey.W && Snake[0].Direction != MovementDirection.Down)
                {
                    Snake[0].Direction = MovementDirection.Up;
                }

                else if (args.VirtualKey == VirtualKey.S && Snake[0].Direction != MovementDirection.Up)
                {
                    Snake[0].Direction = MovementDirection.Down;
                }

                else if (args.VirtualKey == VirtualKey.A && Snake[0].Direction != MovementDirection.Right)
                {
                    Snake[0].Direction = MovementDirection.Left;
                }

                else if (args.VirtualKey == VirtualKey.D && Snake[0].Direction != MovementDirection.Left)
                {
                    Snake[0].Direction = MovementDirection.Right;
                }
            }

            if (args.VirtualKey == VirtualKey.T)
            {
                // I love the tenary opperator.
                currentGameState = (currentGameState == GameState.Paused) ? GameState.InGame : GameState.Paused;
            }
        }

        /// <summary>
        /// Get the first controller, Add the gamepad added and disconnected events.
        /// </summary>
        private void InitiateGamepad()
        {
            Gamepad.GamepadAdded += (object sender, Gamepad e) =>
            {
                lock (myLock)
                {
                    mainGamepad = e;
                    xboxController = true;
                }
            };

            Gamepad.GamepadRemoved += (object sender, Gamepad e) =>
            {
                lock (myLock)
                {
                    mainGamepad = null;
                    xboxController = false;
                }
            };

            lock (myLock)
            {
                // Find the first gamepad.
                foreach (var gamepad in Gamepad.Gamepads)
                {
                    if (!mainGamepad.Equals(gamepad))
                    {
                        mainGamepad = gamepad;
                        xboxController = true;
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Reads gamepad input.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void GamePad_Tick (object sender, Object e)
        {
            reading = mainGamepad.GetCurrentReading();

            if(currentGameState == GameState.InGame)
            {
                if (reading.Buttons == GamepadButtons.DPadUp && Snake[0].Direction != MovementDirection.Down)
                {
                    Snake[0].Direction = MovementDirection.Up;
                }

                else if (reading.Buttons == GamepadButtons.DPadDown && Snake[0].Direction != MovementDirection.Up)
                {
                    Snake[0].Direction = MovementDirection.Down;
                }

                else if (reading.Buttons == GamepadButtons.DPadLeft && Snake[0].Direction != MovementDirection.Right)
                {
                    Snake[0].Direction = MovementDirection.Left;
                }

                else if (reading.Buttons == GamepadButtons.DPadRight && Snake[0].Direction != MovementDirection.Left)
                {
                    Snake[0].Direction = MovementDirection.Right;
                }
            }

            if (reading.Buttons == GamepadButtons.Menu)
            {
                currentGameState = (currentGameState == GameState.Paused) ? GameState.InGame : GameState.Paused;
            }
        }

        #region Debug Stuff

        /*
        /// <summary>
        /// Debug up movement.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UpDebug_Click(object sender, RoutedEventArgs e)
        {
            if (currentGameState == GameState.InGame && Snake[0].Direction != MovementDirection.Down)
            {
                Snake[0].Direction = MovementDirection.Up;
            }
        }

        /// <summary>
        /// Debug left movement.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LeftDebug_Click(object sender, RoutedEventArgs e)
        {
            if (currentGameState == GameState.InGame && Snake[0].Direction != MovementDirection.Right)
            {
                Snake[0].Direction = MovementDirection.Left;
            }
        }

        /// <summary>
        /// Debug down movement.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DownDebug_Click(object sender, RoutedEventArgs e)
        {
            if (currentGameState == GameState.InGame && Snake[0].Direction != MovementDirection.Up)
            {
                Snake[0].Direction = MovementDirection.Down;
            }
        }

        /// <summary>
        /// Debug right movment.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RightDebug_Click(object sender, RoutedEventArgs e)
        {
            if (currentGameState == GameState.InGame && Snake[0].Direction != MovementDirection.Left)
            {
                Snake[0].Direction = MovementDirection.Right;
            }
        }
        */

        #endregion
    }
}
